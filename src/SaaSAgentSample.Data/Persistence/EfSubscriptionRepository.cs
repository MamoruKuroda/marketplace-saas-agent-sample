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

    public async Task<IReadOnlyList<Subscription>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Order client-side: SQLite (the arm64/dev fallback provider) cannot ORDER BY a
        // DateTimeOffset column, so sorting in the database would throw on that provider.
        // The subscription table is small, so a client-side sort is acceptable and keeps
        // the behavior identical across SQL Server, SQLite, and InMemory.
        var items = await _db.Subscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await _db.Subscriptions.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
