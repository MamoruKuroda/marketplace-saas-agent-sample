using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Data.Persistence;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Web.Services;

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

    /// <summary>Reads the authoritative subscription state directly from the store.</summary>
    public async Task<Subscription?> LoadAsync(string marketplaceSubscriptionId)
    {
        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        return await repository.GetByMarketplaceSubscriptionIdAsync(marketplaceSubscriptionId);
    }

    /// <summary>
    /// Triggers activation the same way the buyer landing does (LandingService), which calls the
    /// emulator's Activate API over HTTP and transitions the local record.
    /// </summary>
    public async Task ActivateAsync(string marketplaceSubscriptionId, string planId)
    {
        using var scope = Services.CreateScope();
        var landing = scope.ServiceProvider.GetRequiredService<LandingService>();
        await landing.ActivateAsync(marketplaceSubscriptionId, planId, quantity: null);
    }
}

public class SyntheticL2LifecycleTests
{
    private const string SubId = "sub-e2e";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Full_subscription_lifecycle_over_http()
    {
        await using var emulator = await FulfillmentEmulatorStub.StartAsync();
        using var factory = new L2AppFactory(emulator.BaseUrl + "/api");
        using var client = factory.CreateClient();

        // 1. Resolve: buyer opens the landing page with a purchase token. The app calls the
        //    emulator's resolve API over HTTP and records the subscription as pending.
        (await client.GetAsync("/?token=" + Uri.EscapeDataString("synthetic-token"))).EnsureSuccessStatusCode();

        var resolved = await factory.LoadAsync(SubId);
        Assert.NotNull(resolved);
        Assert.Equal(SubscriptionState.PendingFulfillmentStart, resolved!.State);

        // 2. Activate (buyer landing): the app calls the emulator's Activate API over HTTP.
        await factory.ActivateAsync(SubId, resolved.PlanId);
        Assert.Equal(1, emulator.ActivateCount);
        Assert.Equal(SubscriptionState.Subscribed, (await factory.LoadAsync(SubId))!.State);

        // 3. ChangePlan webhook: the emulator notifies; the app authorizes via Get Operation
        //    (over HTTP), changes the plan, and acknowledges via Patch Operation (over HTTP).
        emulator.RegisterOperation("op-changeplan", SubId, "ChangePlan", planId: "gold");
        (await PostWebhookAsync(client, "op-changeplan", SubId, "ChangePlan", planId: "gold")).EnsureSuccessStatusCode();
        var afterChangePlan = await factory.LoadAsync(SubId);
        Assert.Equal("gold", afterChangePlan!.PlanId);
        Assert.Equal(SubscriptionState.Subscribed, afterChangePlan.State);
        Assert.Contains(emulator.Patches, p => p.OperationId == "op-changeplan" && p.Status == "Success");

        // 4. Suspend webhook.
        emulator.RegisterOperation("op-suspend", SubId, "Suspend");
        (await PostWebhookAsync(client, "op-suspend", SubId, "Suspend")).EnsureSuccessStatusCode();
        Assert.Equal(SubscriptionState.Suspended, (await factory.LoadAsync(SubId))!.State);

        // 5. Reinstate webhook.
        emulator.RegisterOperation("op-reinstate", SubId, "Reinstate");
        (await PostWebhookAsync(client, "op-reinstate", SubId, "Reinstate")).EnsureSuccessStatusCode();
        Assert.Equal(SubscriptionState.Subscribed, (await factory.LoadAsync(SubId))!.State);

        // 6. Unsubscribe webhook (terminal).
        emulator.RegisterOperation("op-unsubscribe", SubId, "Unsubscribe");
        (await PostWebhookAsync(client, "op-unsubscribe", SubId, "Unsubscribe")).EnsureSuccessStatusCode();
        Assert.Equal(SubscriptionState.Unsubscribed, (await factory.LoadAsync(SubId))!.State);
    }

    [Fact]
    public async Task Webhook_without_a_matching_operation_is_rejected_and_state_is_unchanged()
    {
        await using var emulator = await FulfillmentEmulatorStub.StartAsync();
        using var factory = new L2AppFactory(emulator.BaseUrl + "/api");
        using var client = factory.CreateClient();

        // Bring a subscription to Subscribed.
        (await client.GetAsync("/?token=t")).EnsureSuccessStatusCode();
        var resolved = await factory.LoadAsync(SubId);
        await factory.ActivateAsync(SubId, resolved!.PlanId);

        // Post a Suspend webhook whose operation the emulator does not know about. The app's
        // Get Operation authorization check (over HTTP) fails, so the payload is rejected.
        var response = await PostWebhookAsync(client, "op-unknown", SubId, "Suspend");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(SubscriptionState.Subscribed, (await factory.LoadAsync(SubId))!.State);
    }

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
