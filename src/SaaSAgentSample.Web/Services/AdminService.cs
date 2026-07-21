using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Web.Services;

/// <summary>Result of an admin-initiated activation.</summary>
public enum AdminActivationResult
{
    Activated,
    AlreadyActive,
    NotFound,
    InvalidState,
}

/// <summary>
/// Backs the minimal publisher admin. Read operations project the authoritative state store;
/// the single write operation (Activate) is an explicit, confirmed action that calls the
/// Fulfillment Activate API and then transitions the local aggregate. The state store remains
/// the single source of truth; entitlement is never fabricated.
/// </summary>
public sealed class AdminService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IFulfillmentClient _fulfillment;
    private readonly ILogger<AdminService> _logger;

    public AdminService(ISubscriptionRepository repository, IFulfillmentClient fulfillment, ILogger<AdminService> logger)
    {
        _repository = repository;
        _fulfillment = fulfillment;
        _logger = logger;
    }

    public Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
        => _repository.ListAsync(cancellationToken);

    public Task<Subscription?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Activates a subscription that is awaiting fulfillment: calls the Fulfillment Activate API,
    /// then transitions the local record to Subscribed. Idempotent when already Subscribed.
    /// </summary>
    public async Task<AdminActivationResult> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subscription = await _repository.GetByIdAsync(id, cancellationToken);
        if (subscription is null)
        {
            return AdminActivationResult.NotFound;
        }

        if (subscription.State == SubscriptionState.Subscribed)
        {
            return AdminActivationResult.AlreadyActive; // idempotent
        }

        if (subscription.State != SubscriptionState.PendingFulfillmentStart)
        {
            return AdminActivationResult.InvalidState;
        }

        await _fulfillment.ActivateAsync(
            subscription.MarketplaceSubscriptionId,
            new ActivateRequest { PlanId = subscription.PlanId },
            cancellationToken);

        subscription.Activate(DateTimeOffset.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin activated subscription {SubscriptionId}.", subscription.MarketplaceSubscriptionId);
        return AdminActivationResult.Activated;
    }
}
