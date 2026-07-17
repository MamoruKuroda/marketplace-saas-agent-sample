using Microsoft.Extensions.Logging.Abstractions;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Tests.LandingTests;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Tests.AdminTests;

public class AdminServiceTests
{
    private static Subscription Pending(string marketplaceId = "mkt-1", string plan = "silver")
        => new(Guid.NewGuid(), marketplaceId, "offer1", plan, DateTimeOffset.UtcNow);

    private static Subscription InState(SubscriptionState target, string marketplaceId = "mkt-1")
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Pending(marketplaceId);
        switch (target)
        {
            case SubscriptionState.Subscribed:
                subscription.Activate(now);
                break;
            case SubscriptionState.Suspended:
                subscription.Activate(now);
                subscription.Suspend(now);
                break;
            case SubscriptionState.Unsubscribed:
                subscription.Unsubscribe(now);
                break;
        }

        return subscription;
    }

    private static (AdminService Service, InMemorySubscriptionRepository Repository, FakeFulfillmentClient Fulfillment) Build(params Subscription[] seed)
    {
        var repository = new InMemorySubscriptionRepository();
        foreach (var s in seed)
        {
            repository.AddAsync(s).GetAwaiter().GetResult();
        }

        var fulfillment = new FakeFulfillmentClient();
        var service = new AdminService(repository, fulfillment, NullLogger<AdminService>.Instance);
        return (service, repository, fulfillment);
    }

    [Fact]
    public async Task ListSubscriptionsAsync_returns_all_records()
    {
        var (service, _, _) = Build(Pending("mkt-1"), Pending("mkt-2"));

        var all = await service.ListSubscriptionsAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetSubscriptionAsync_returns_record_by_id()
    {
        var seeded = Pending("mkt-1");
        var (service, _, _) = Build(seeded);

        var found = await service.GetSubscriptionAsync(seeded.Id);

        Assert.NotNull(found);
        Assert.Equal(seeded.Id, found!.Id);
    }

    [Fact]
    public async Task ActivateAsync_activates_pending_subscription()
    {
        var seeded = InState(SubscriptionState.PendingFulfillmentStart);
        var (service, repository, fulfillment) = Build(seeded);

        var result = await service.ActivateAsync(seeded.Id);

        Assert.Equal(AdminActivationResult.Activated, result);
        Assert.Equal(1, fulfillment.ActivateCallCount);
        Assert.Equal(seeded.MarketplaceSubscriptionId, fulfillment.LastActivatedSubscriptionId);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
    }

    [Fact]
    public async Task ActivateAsync_returns_NotFound_for_unknown_id()
    {
        var (service, _, fulfillment) = Build();

        var result = await service.ActivateAsync(Guid.NewGuid());

        Assert.Equal(AdminActivationResult.NotFound, result);
        Assert.Equal(0, fulfillment.ActivateCallCount);
    }

    [Fact]
    public async Task ActivateAsync_is_idempotent_when_already_subscribed()
    {
        var seeded = InState(SubscriptionState.Subscribed);
        var (service, repository, fulfillment) = Build(seeded);

        var result = await service.ActivateAsync(seeded.Id);

        Assert.Equal(AdminActivationResult.AlreadyActive, result);
        Assert.Equal(0, fulfillment.ActivateCallCount); // no duplicate Fulfillment call
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
    }

    [Fact]
    public async Task ActivateAsync_rejects_invalid_state()
    {
        var seeded = InState(SubscriptionState.Suspended);
        var (service, repository, fulfillment) = Build(seeded);

        var result = await service.ActivateAsync(seeded.Id);

        Assert.Equal(AdminActivationResult.InvalidState, result);
        Assert.Equal(0, fulfillment.ActivateCallCount);
        Assert.Equal(SubscriptionState.Suspended, repository.Items[0].State); // unchanged
    }
}
