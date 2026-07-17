using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Data.Persistence;
using SaaSAgentSample.Web.Agent;

namespace SaaSAgentSample.Tests.L2;

/// <summary>
/// Hosts the real app (real FulfillmentClient) pointed at the in-repo emulator stub over HTTP,
/// with a per-test InMemory store. appsettings.Testing.json disables auth and relaxes webhook
/// signature validation (the emulator sends unsigned tokens).
/// </summary>
internal sealed class L2AppFactory : WebApplicationFactory<Program>
{
    private readonly string _fulfillmentBaseUrl;
    private readonly string _databaseName = "L2-" + Guid.NewGuid();

    public L2AppFactory(string fulfillmentBaseUrl) => _fulfillmentBaseUrl = fulfillmentBaseUrl;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<SaasDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.IsGenericType &&
                    d.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1" &&
                    d.ServiceType.GenericTypeArguments[0] == typeof(SaasDbContext))).ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<SaasDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Point the real FulfillmentClient at the emulator stub (dynamic port).
            services.PostConfigure<FulfillmentOptions>(o => o.BaseUrl = _fulfillmentBaseUrl);
        });
    }
}

public class SyntheticL2LifecycleTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Full_subscription_lifecycle_over_http()
    {
        await using var emulator = await FulfillmentEmulatorStub.StartAsync();
        using var factory = new L2AppFactory(emulator.BaseUrl + "/api");
        using var client = factory.CreateClient();

        // 1. Resolve: buyer opens the landing page with a purchase token. The app calls the
        //    emulator's resolve API over HTTP and records the subscription as pending.
        var landing = await client.GetAsync("/?token=" + Uri.EscapeDataString("synthetic-token"));
        landing.EnsureSuccessStatusCode();

        var subscriptions = await client.GetFromJsonAsync<SubscriptionDto[]>("/api/subscriptions", Web);
        var subscription = Assert.Single(subscriptions!);
        Assert.Equal("sub-e2e", subscription.MarketplaceSubscriptionId);
        Assert.Equal("PendingFulfillmentStart", subscription.State);
        var id = subscription.Id;

        // 2. Activate (explicit confirmation): the app calls the emulator's activate API over HTTP.
        var activate = await client.PostAsJsonAsync($"/api/subscriptions/{id}/activate", new { confirm = true }, Web);
        activate.EnsureSuccessStatusCode();
        Assert.Equal(1, emulator.ActivateCount);
        Assert.Equal("Subscribed", (await GetSubscriptionAsync(client, id)).State);

        // 3. ChangePlan webhook: the emulator notifies; the app authorizes via Get Operation
        //    (over HTTP), changes the plan, and acknowledges via Patch Operation (over HTTP).
        emulator.RegisterOperation("op-changeplan", "sub-e2e", "ChangePlan", planId: "gold");
        (await PostWebhookAsync(client, "op-changeplan", "sub-e2e", "ChangePlan", planId: "gold")).EnsureSuccessStatusCode();
        var afterChangePlan = await GetSubscriptionAsync(client, id);
        Assert.Equal("gold", afterChangePlan.PlanId);
        Assert.Equal("Subscribed", afterChangePlan.State);
        Assert.Contains(emulator.Patches, p => p.OperationId == "op-changeplan" && p.Status == "Success");

        // 4. Suspend webhook.
        emulator.RegisterOperation("op-suspend", "sub-e2e", "Suspend");
        (await PostWebhookAsync(client, "op-suspend", "sub-e2e", "Suspend")).EnsureSuccessStatusCode();
        Assert.Equal("Suspended", (await GetSubscriptionAsync(client, id)).State);

        // 5. Reinstate webhook.
        emulator.RegisterOperation("op-reinstate", "sub-e2e", "Reinstate");
        (await PostWebhookAsync(client, "op-reinstate", "sub-e2e", "Reinstate")).EnsureSuccessStatusCode();
        Assert.Equal("Subscribed", (await GetSubscriptionAsync(client, id)).State);

        // 6. Unsubscribe webhook (terminal).
        emulator.RegisterOperation("op-unsubscribe", "sub-e2e", "Unsubscribe");
        (await PostWebhookAsync(client, "op-unsubscribe", "sub-e2e", "Unsubscribe")).EnsureSuccessStatusCode();
        Assert.Equal("Unsubscribed", (await GetSubscriptionAsync(client, id)).State);
    }

    [Fact]
    public async Task Webhook_without_a_matching_operation_is_rejected_and_state_is_unchanged()
    {
        await using var emulator = await FulfillmentEmulatorStub.StartAsync();
        using var factory = new L2AppFactory(emulator.BaseUrl + "/api");
        using var client = factory.CreateClient();

        // Bring a subscription to Subscribed.
        (await client.GetAsync("/?token=t")).EnsureSuccessStatusCode();
        var id = (await client.GetFromJsonAsync<SubscriptionDto[]>("/api/subscriptions", Web))!.Single().Id;
        (await client.PostAsJsonAsync($"/api/subscriptions/{id}/activate", new { confirm = true }, Web)).EnsureSuccessStatusCode();

        // Post a Suspend webhook whose operation the emulator does not know about. The app's
        // Get Operation authorization check (over HTTP) fails, so the payload is rejected.
        var response = await PostWebhookAsync(client, "op-unknown", "sub-e2e", "Suspend");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Subscribed", (await GetSubscriptionAsync(client, id)).State);
    }

    private static async Task<SubscriptionDto> GetSubscriptionAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<SubscriptionDto>($"/api/subscriptions/{id}", Web))!;

    private static Task<HttpResponseMessage> PostWebhookAsync(
        HttpClient client, string operationId, string subscriptionId, string action, string? planId = null, int? quantity = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + UnsignedJwt());

        var payload = new Dictionary<string, object?>
        {
            ["id"] = operationId,
            ["subscriptionId"] = subscriptionId,
            ["action"] = action,
            ["status"] = "InProgress",
        };
        if (planId is not null)
        {
            payload["planId"] = planId;
        }

        if (quantity is not null)
        {
            payload["quantity"] = quantity;
        }

        request.Content = JsonContent.Create(payload, options: Web);
        return client.SendAsync(request);
    }

    // The emulator sends unsigned notifications; the app parses them leniently in this
    // environment (Fulfillment:Webhook:RequireSignedToken=false in appsettings.Testing.json).
    private static string UnsignedJwt()
    {
        static string B64(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64("{\"appid\":\"20e940b3-4c07-4bc1-a733-45f7c7a3d0e3\",\"aud\":\"test\"}");
        return header + "." + payload + ".";
    }
}
