using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Data.Persistence;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Tests.LandingTests;
using SaaSAgentSample.Web.Agent;

namespace SaaSAgentSample.Tests.AgentTests;

/// <summary>
/// Hosts the Web app with an InMemory state store, authentication disabled, and a fake
/// Fulfillment client so the tool boundary can be exercised over real HTTP.
/// </summary>
internal sealed class AgentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "AgentApiTests-" + Guid.NewGuid();

    public FakeFulfillmentClient Fulfillment { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // appsettings.Testing.json switches the store to InMemory and disables auth, using the
        // app's own environment-specific config layering (reliable with the minimal hosting model).
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Give each factory its own InMemory database so tests are isolated from one another.
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

            services.RemoveAll<IFulfillmentClient>();
            services.AddSingleton<IFulfillmentClient>(Fulfillment);
        });
    }

    public async Task<Guid> SeedPendingAsync(string marketplaceId = "mkt-1", string plan = "silver")
    {
        using var scope = Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var subscription = new Subscription(Guid.NewGuid(), marketplaceId, "offer1", plan, DateTimeOffset.UtcNow);
        await repo.AddAsync(subscription);
        await repo.SaveChangesAsync();
        return subscription.Id;
    }
}

public class AgentApiTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Get_tools_returns_the_catalog()
    {
        using var factory = new AgentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tools");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "list_subscriptions", "get_subscription", "activate_subscription" }, names);
    }

    [Fact]
    public async Task Openapi_document_is_served()
    {
        using var factory = new AgentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("openapi", out _));
    }

    [Fact]
    public async Task List_subscriptions_reflects_the_state_store()
    {
        using var factory = new AgentApiFactory();
        await factory.SeedPendingAsync("mkt-list");
        using var client = factory.CreateClient();

        var items = await client.GetFromJsonAsync<SubscriptionDto[]>("/api/subscriptions", Web);

        Assert.NotNull(items);
        var item = Assert.Single(items!);
        Assert.Equal("mkt-list", item.MarketplaceSubscriptionId);
        Assert.Equal(nameof(SubscriptionState.PendingFulfillmentStart), item.State);
    }

    [Fact]
    public async Task Get_subscription_returns_404_for_unknown_id()
    {
        using var factory = new AgentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/subscriptions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Activate_without_confirmation_is_rejected_and_does_not_change_state()
    {
        using var factory = new AgentApiFactory();
        var id = await factory.SeedPendingAsync("mkt-noconfirm");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/subscriptions/{id}/activate", new { confirm = false }, Web);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Fulfillment.ActivateCallCount); // guardrail: no state change without confirmation

        var after = await client.GetFromJsonAsync<SubscriptionDto>($"/api/subscriptions/{id}", Web);
        Assert.Equal(nameof(SubscriptionState.PendingFulfillmentStart), after!.State);
    }

    [Fact]
    public async Task Activate_with_confirmation_activates_the_subscription()
    {
        using var factory = new AgentApiFactory();
        var id = await factory.SeedPendingAsync("mkt-confirm");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/subscriptions/{id}/activate", new { confirm = true }, Web);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, factory.Fulfillment.ActivateCallCount);

        var after = await client.GetFromJsonAsync<SubscriptionDto>($"/api/subscriptions/{id}", Web);
        Assert.Equal(nameof(SubscriptionState.Subscribed), after!.State);
    }
}
