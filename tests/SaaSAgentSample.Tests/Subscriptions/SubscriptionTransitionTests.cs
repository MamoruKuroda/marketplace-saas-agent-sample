using SaaSAgentSample.Core.Subscriptions;

namespace SaaSAgentSample.Tests.Subscriptions;

/// <summary>
/// Verifies the transition rules enforced by <see cref="Subscription"/> match
/// the four-state SaaS fulfillment life cycle.
/// </summary>
public class SubscriptionTransitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    private static Subscription NewSubscription()
        => new(Guid.NewGuid(), "mkt-sub-1", "offer-1", "plan-basic", Now);

    [Fact]
    public void NewSubscription_starts_in_PendingFulfillmentStart()
    {
        var sub = NewSubscription();

        Assert.Equal(SubscriptionState.PendingFulfillmentStart, sub.State);
        Assert.Equal(Now, sub.CreatedAt);
        Assert.Equal(Now, sub.UpdatedAt);
    }

    [Fact]
    public void Activate_from_PendingFulfillmentStart_moves_to_Subscribed()
    {
        var sub = NewSubscription();
        var later = Now.AddMinutes(5);

        sub.Activate(later);

        Assert.Equal(SubscriptionState.Subscribed, sub.State);
        Assert.Equal(later, sub.UpdatedAt);
    }

    [Fact]
    public void Suspend_from_Subscribed_moves_to_Suspended()
    {
        var sub = NewSubscription();
        sub.Activate(Now);

        sub.Suspend(Now.AddMinutes(1));

        Assert.Equal(SubscriptionState.Suspended, sub.State);
    }

    [Fact]
    public void Reinstate_from_Suspended_moves_to_Subscribed()
    {
        var sub = NewSubscription();
        sub.Activate(Now);
        sub.Suspend(Now);

        sub.Reinstate(Now.AddMinutes(1));

        Assert.Equal(SubscriptionState.Subscribed, sub.State);
    }

    [Theory]
    [InlineData(SubscriptionState.PendingFulfillmentStart)]
    [InlineData(SubscriptionState.Subscribed)]
    [InlineData(SubscriptionState.Suspended)]
    public void Unsubscribe_moves_any_non_terminal_state_to_Unsubscribed(SubscriptionState from)
    {
        var sub = NewSubscription();
        MoveTo(sub, from);

        sub.Unsubscribe(Now.AddMinutes(1));

        Assert.Equal(SubscriptionState.Unsubscribed, sub.State);
    }

    [Fact]
    public void Unsubscribe_from_Unsubscribed_is_rejected()
    {
        var sub = NewSubscription();
        sub.Unsubscribe(Now);

        var ex = Assert.Throws<InvalidSubscriptionTransitionException>(
            () => sub.Unsubscribe(Now.AddMinutes(1)));
        Assert.Equal(SubscriptionState.Unsubscribed, ex.From);
        Assert.Equal(nameof(Subscription.Unsubscribe), ex.Operation);
    }

    [Fact]
    public void Activate_from_Subscribed_is_rejected()
    {
        var sub = NewSubscription();
        sub.Activate(Now);

        Assert.Throws<InvalidSubscriptionTransitionException>(() => sub.Activate(Now));
    }

    [Fact]
    public void Suspend_from_PendingFulfillmentStart_is_rejected()
    {
        var sub = NewSubscription();

        Assert.Throws<InvalidSubscriptionTransitionException>(() => sub.Suspend(Now));
    }

    [Fact]
    public void Reinstate_from_Subscribed_is_rejected()
    {
        var sub = NewSubscription();
        sub.Activate(Now);

        Assert.Throws<InvalidSubscriptionTransitionException>(() => sub.Reinstate(Now));
    }

    [Fact]
    public void ChangePlan_from_Subscribed_updates_plan_and_leaves_state()
    {
        var sub = NewSubscription();
        sub.Activate(Now);

        sub.ChangePlan("plan-pro", Now.AddMinutes(2));

        Assert.Equal("plan-pro", sub.PlanId);
        Assert.Equal(SubscriptionState.Subscribed, sub.State);
    }

    [Fact]
    public void ChangePlan_from_non_Subscribed_is_rejected()
    {
        var sub = NewSubscription();

        Assert.Throws<InvalidSubscriptionTransitionException>(
            () => sub.ChangePlan("plan-pro", Now));
    }

    [Fact]
    public void ChangePlan_requires_non_empty_plan_id()
    {
        var sub = NewSubscription();
        sub.Activate(Now);

        Assert.Throws<ArgumentException>(() => sub.ChangePlan("  ", Now));
    }

    [Fact]
    public void Constructor_requires_non_empty_identifiers()
    {
        Assert.Throws<ArgumentException>(
            () => new Subscription(Guid.Empty, "mkt", "offer", "plan", Now));
        Assert.Throws<ArgumentException>(
            () => new Subscription(Guid.NewGuid(), " ", "offer", "plan", Now));
    }

    private static void MoveTo(Subscription sub, SubscriptionState target)
    {
        switch (target)
        {
            case SubscriptionState.PendingFulfillmentStart:
                return;
            case SubscriptionState.Subscribed:
                sub.Activate(Now);
                return;
            case SubscriptionState.Suspended:
                sub.Activate(Now);
                sub.Suspend(Now);
                return;
            case SubscriptionState.Unsubscribed:
                sub.Unsubscribe(Now);
                return;
        }
    }
}
