using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment.Models;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Tests.LandingTests;

public class LandingServiceTests
{
    private static ResolvedSubscription SampleResolved(string subscriptionId = "sub-1") => new()
    {
        Id = subscriptionId,
        OfferId = "offer1",
        PlanId = "silver",
        Quantity = 5,
        Subscription = new FulfillmentSubscription
        {
            Id = subscriptionId,
            Name = "Contoso",
            OfferId = "offer1",
            PlanId = "silver",
            SaasSubscriptionStatus = "PendingFulfillmentStart",
        },
    };

    [Fact]
    public async Task ResolveAsync_records_pending_subscription()
    {
        var repository = new InMemorySubscriptionRepository();
        var service = new LandingService(new FakeFulfillmentClient(SampleResolved()), repository);

        var result = await service.ResolveAsync("token");

        Assert.NotNull(result);
        var stored = Assert.Single(repository.Items);
        Assert.Equal("sub-1", stored.MarketplaceSubscriptionId);
        Assert.Equal(SubscriptionState.PendingFulfillmentStart, stored.State);
    }

    [Fact]
    public async Task ResolveAsync_does_not_duplicate_existing_record()
    {
        var repository = new InMemorySubscriptionRepository();
        var service = new LandingService(new FakeFulfillmentClient(SampleResolved()), repository);

        await service.ResolveAsync("token");
        await service.ResolveAsync("token");

        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ActivateAsync_activates_and_transitions_to_subscribed()
    {
        var repository = new InMemorySubscriptionRepository();
        var fulfillment = new FakeFulfillmentClient(SampleResolved());
        var service = new LandingService(fulfillment, repository);
        await service.ResolveAsync("token"); // creates the pending record

        var result = await service.ActivateAsync("sub-1", "silver", 5);

        Assert.Equal(LandingActivationResult.Activated, result);
        Assert.Equal(1, fulfillment.ActivateCallCount);
        Assert.Equal("sub-1", fulfillment.LastActivatedSubscriptionId);
        Assert.Equal(SubscriptionState.Subscribed, Assert.Single(repository.Items).State);
    }

    [Fact]
    public async Task ActivateAsync_returns_NotFound_when_subscription_missing()
    {
        var repository = new InMemorySubscriptionRepository();
        var service = new LandingService(new FakeFulfillmentClient(), repository);

        var result = await service.ActivateAsync("missing", "silver", null);

        Assert.Equal(LandingActivationResult.NotFound, result);
    }

    [Fact]
    public async Task ActivateAsync_is_idempotent_when_already_subscribed()
    {
        var repository = new InMemorySubscriptionRepository();
        var service = new LandingService(new FakeFulfillmentClient(SampleResolved()), repository);
        await service.ResolveAsync("token");
        await service.ActivateAsync("sub-1", "silver", 5); // -> Subscribed

        var result = await service.ActivateAsync("sub-1", "silver", 5); // again

        Assert.Equal(LandingActivationResult.Activated, result);
        Assert.Equal(SubscriptionState.Subscribed, Assert.Single(repository.Items).State);
    }
}
