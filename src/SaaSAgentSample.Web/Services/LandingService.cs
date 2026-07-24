using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Web.Services;

/// <summary>Result of a landing-page activation attempt.</summary>
public enum LandingActivationResult
{
    Activated,
    NotFound,
    InvalidState,
}

/// <summary>
/// Orchestrates the buyer landing flow over the Fulfillment client (Resolve/Activate) and the
/// authoritative state store. Kept free of ASP.NET types so it is unit-testable. The state DB
/// remains the single source of truth; activation is an explicit, confirmed action.
/// </summary>
public sealed class LandingService
{
    private readonly IFulfillmentClient _fulfillment;
    private readonly ISubscriptionRepository _repository;

    public LandingService(IFulfillmentClient fulfillment, ISubscriptionRepository repository)
    {
        _fulfillment = fulfillment;
        _repository = repository;
    }

    /// <summary>
    /// Resolves a marketplace purchase token and records the subscription locally in
    /// <see cref="SubscriptionState.PendingFulfillmentStart"/> if not already tracked.
    /// </summary>
    public async Task<ResolvedSubscription?> ResolveAsync(string marketplaceToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceToken);

        var resolved = await _fulfillment.ResolveAsync(marketplaceToken, cancellationToken);
        var marketplaceSubscriptionId = resolved?.Subscription?.Id ?? resolved?.Id;
        if (resolved is null || string.IsNullOrEmpty(marketplaceSubscriptionId))
        {
            return resolved;
        }

        var existing = await _repository.GetByMarketplaceSubscriptionIdAsync(marketplaceSubscriptionId, cancellationToken);
        if (existing is null)
        {
            var subscription = new Subscription(
                Guid.NewGuid(),
                marketplaceSubscriptionId,
                resolved.OfferId ?? resolved.Subscription?.OfferId ?? string.Empty,
                resolved.PlanId ?? resolved.Subscription?.PlanId ?? string.Empty,
                DateTimeOffset.UtcNow,
                resolved.SubscriptionName ?? resolved.Subscription?.Name);

            await _repository.AddAsync(subscription, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return resolved;
    }

    /// <summary>
    /// Activates a subscription: calls the Activate API, then transitions the local record from
    /// PendingFulfillmentStart to Subscribed. Idempotent when the record is already Subscribed.
    /// </summary>
    public async Task<LandingActivationResult> ActivateAsync(
        string marketplaceSubscriptionId,
        string planId,
        int? quantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceSubscriptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        await _fulfillment.ActivateAsync(
            marketplaceSubscriptionId,
            new ActivateRequest { PlanId = planId, Quantity = quantity },
            cancellationToken);

        var subscription = await _repository.GetByMarketplaceSubscriptionIdAsync(marketplaceSubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return LandingActivationResult.NotFound;
        }

        switch (subscription.State)
        {
            case SubscriptionState.Subscribed:
                return LandingActivationResult.Activated; // idempotent

            case SubscriptionState.PendingFulfillmentStart:
                subscription.Activate(DateTimeOffset.UtcNow);
                await _repository.SaveChangesAsync(cancellationToken);
                return LandingActivationResult.Activated;

            default:
                return LandingActivationResult.InvalidState;
        }
    }
}
