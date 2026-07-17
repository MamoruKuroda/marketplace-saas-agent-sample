using SaaSAgentSample.Core.Subscriptions;

namespace SaaSAgentSample.Web.Agent;

/// <summary>
/// Public, PII-free projection of a subscription used by the agent tool boundary and the
/// JSON operations API. Contains only state-store fields — never beneficiary/purchaser data.
/// </summary>
public sealed record SubscriptionDto
{
    public required Guid Id { get; init; }
    public required string MarketplaceSubscriptionId { get; init; }
    public required string OfferId { get; init; }
    public required string PlanId { get; init; }
    public required string State { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public static SubscriptionDto FromDomain(Subscription subscription) => new()
    {
        Id = subscription.Id,
        MarketplaceSubscriptionId = subscription.MarketplaceSubscriptionId,
        OfferId = subscription.OfferId,
        PlanId = subscription.PlanId,
        State = subscription.State.ToString(),
        CreatedAt = subscription.CreatedAt,
        UpdatedAt = subscription.UpdatedAt,
    };
}

/// <summary>
/// Body of the activate operation. Confirmation is mandatory: a state change must be explicitly
/// confirmed by the caller, so the model can never activate a subscription on its own.
/// </summary>
public sealed record ActivateSubscriptionRequest
{
    public bool Confirm { get; init; }
}
