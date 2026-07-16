namespace SaaSAgentSample.Core.Subscriptions;

/// <summary>
/// Persistence abstraction for <see cref="Subscription"/> aggregates. The
/// concrete implementation lives in <c>SaaSAgentSample.Data</c> so the domain
/// stays infrastructure-agnostic.
/// </summary>
public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Subscription?> GetByMarketplaceSubscriptionIdAsync(
        string marketplaceSubscriptionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
