using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Tests.FulfillmentTests;

public class FulfillmentClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string responseJson = "") : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(status);
            if (!string.IsNullOrEmpty(responseJson))
            {
                response.Content = new StringContent(responseJson, Encoding.UTF8, "application/json");
            }

            return response;
        }
    }

    private static FulfillmentClient CreateClient(StubHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new FulfillmentOptions { BaseUrl = "https://example.test/api", ApiVersion = "2018-08-31" });
        return new FulfillmentClient(httpClient, new DevNullMarketplaceTokenProvider(), options);
    }

    [Fact]
    public async Task ResolveAsync_sends_marketplace_token_and_parses_response()
    {
        const string json = """
        {
          "id": "sub-guid",
          "subscriptionName": "Contoso",
          "offerId": "offer1",
          "planId": "silver",
          "quantity": 20,
          "subscription": { "id": "sub-guid", "saasSubscriptionStatus": "PendingFulfillmentStart" }
        }
        """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.ResolveAsync("the-token");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("/api/saas/subscriptions/resolve?api-version=2018-08-31", handler.LastRequest.RequestUri!.ToString());
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-ms-marketplace-token", out var tokens));
        Assert.Equal("the-token", Assert.Single(tokens!));
        Assert.Equal("sub-guid", result!.Id);
        Assert.Equal("PendingFulfillmentStart", result.Subscription!.SaasSubscriptionStatus);
    }

    [Fact]
    public async Task ActivateAsync_posts_plan_and_quantity()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var client = CreateClient(handler);

        await client.ActivateAsync("sub1", new ActivateRequest { PlanId = "silver", Quantity = 5 });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("/api/saas/subscriptions/sub1/activate?api-version=2018-08-31", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("\"planId\":\"silver\"", handler.LastBody);
        Assert.Contains("\"quantity\":5", handler.LastBody);
    }

    [Fact]
    public async Task GetOperationAsync_parses_operation()
    {
        const string json = """
        { "id": "op1", "action": "ChangePlan", "status": "InProgress" }
        """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var op = await client.GetOperationAsync("sub1", "op1");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Contains("/api/saas/subscriptions/sub1/operations/op1?api-version=2018-08-31", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("ChangePlan", op!.Action);
        Assert.Equal("InProgress", op.Status);
    }

    [Fact]
    public async Task PatchOperationAsync_sends_status()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var client = CreateClient(handler);

        await client.PatchOperationAsync("sub1", "op1", new PatchOperationRequest { Status = "Success" });

        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Contains("/api/saas/subscriptions/sub1/operations/op1?api-version=2018-08-31", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("\"status\":\"Success\"", handler.LastBody);
    }

    [Fact]
    public async Task Non_success_status_throws_FulfillmentApiException()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<FulfillmentApiException>(() => client.ResolveAsync("tok"));
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }
}
