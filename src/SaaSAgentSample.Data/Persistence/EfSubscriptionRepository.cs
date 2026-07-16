using Microsoft.EntityFrameworkCore;
using SaaSAgentSample.Core.Subscriptions;

namespace SaaSAgentSample.Data.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="ISubscriptionRepository"/>.
/// Change tracking is enabled, so callers mutate the aggregate and then call
/// <see cref="SaveChangesAsync"/>.
/// </summary>
public sealed class EfSubscriptionRepository : ISubscriptionRepository
{
    private readonly SaasDbContext _db;

    public EfSubscriptionRepository(SaasDbContext db)
    {
        _db = db;
    }

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Subscription?> GetByMarketplaceSubscriptionIdAsync(
        string marketplaceSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceSubscriptionId);

        return _db.Subscriptions.FirstOrDefaultAsync(
            s => s.MarketplaceSubscriptionId == marketplaceSubscriptionId,
            cancellationToken);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await _db.Subscriptions.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
