namespace SaaSAgentSample.Core.Subscriptions;

/// <summary>
/// Aggregate root representing a single SaaS subscription persisted in the
/// state store. Enforces the transitions defined by the SaaS fulfillment
/// life cycle so callers cannot put the aggregate into an invalid state.
///
/// Reference: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-life-cycle
/// </summary>
public sealed class Subscription
{
    // Parameterless constructor is required by EF Core materialization.
    private Subscription()
    {
        MarketplaceSubscriptionId = string.Empty;
        OfferId = string.Empty;
        PlanId = string.Empty;
    }

    public Subscription(
        Guid id,
        string marketplaceSubscriptionId,
        string offerId,
        string planId,
        DateTimeOffset createdAt,
        string? name = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty GUID.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceSubscriptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(offerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        Id = id;
        MarketplaceSubscriptionId = marketplaceSubscriptionId;
        Name = name;
        OfferId = offerId;
        PlanId = planId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        State = SubscriptionState.PendingFulfillmentStart;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The marketplace subscription id returned by the Fulfillment API. Stored
    /// as a string because Microsoft treats it as an opaque identifier.
    /// </summary>
    public string MarketplaceSubscriptionId { get; private set; }

    /// <summary>
    /// The buyer-friendly subscription name from the Fulfillment API (Resolve), shown in the
    /// admin UI for correlation. Not authoritative for any transition; may be null.
    /// </summary>
    public string? Name { get; private set; }

    public string OfferId { get; private set; }

    public string PlanId { get; private set; }

    public SubscriptionState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Transition <see cref="SubscriptionState.PendingFulfillmentStart"/> to
    /// <see cref="SubscriptionState.Subscribed"/>. Any other source state is
    /// rejected.
    /// </summary>
    public void Activate(DateTimeOffset at)
    {
        if (State != SubscriptionState.PendingFulfillmentStart)
        {
            throw new InvalidSubscriptionTransitionException(State, nameof(Activate));
        }

        State = SubscriptionState.Subscribed;
        UpdatedAt = at;
    }

    /// <summary>
    /// Transition <see cref="SubscriptionState.Subscribed"/> to
    /// <see cref="SubscriptionState.Suspended"/>.
    /// </summary>
    public void Suspend(DateTimeOffset at)
    {
        if (State != SubscriptionState.Subscribed)
        {
            throw new InvalidSubscriptionTransitionException(State, nameof(Suspend));
        }

        State = SubscriptionState.Suspended;
        UpdatedAt = at;
    }

    /// <summary>
    /// Transition <see cref="SubscriptionState.Suspended"/> back to
    /// <see cref="SubscriptionState.Subscribed"/>.
    /// </summary>
    public void Reinstate(DateTimeOffset at)
    {
        if (State != SubscriptionState.Suspended)
        {
            throw new InvalidSubscriptionTransitionException(State, nameof(Reinstate));
        }

        State = SubscriptionState.Subscribed;
        UpdatedAt = at;
    }

    /// <summary>
    /// Transition any non-terminal state to <see cref="SubscriptionState.Unsubscribed"/>.
    /// Terminal.
    /// </summary>
    public void Unsubscribe(DateTimeOffset at)
    {
        if (State == SubscriptionState.Unsubscribed)
        {
            throw new InvalidSubscriptionTransitionException(State, nameof(Unsubscribe));
        }

        State = SubscriptionState.Unsubscribed;
        UpdatedAt = at;
    }

    /// <summary>
    /// Change the plan while remaining in <see cref="SubscriptionState.Subscribed"/>.
    /// State is unchanged; only <see cref="PlanId"/> is updated.
    /// </summary>
    public void ChangePlan(string newPlanId, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanId);

        if (State != SubscriptionState.Subscribed)
        {
            throw new InvalidSubscriptionTransitionException(State, nameof(ChangePlan));
        }

        PlanId = newPlanId;
        UpdatedAt = at;
    }
}
