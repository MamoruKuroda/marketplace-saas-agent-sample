using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Data.Persistence;

namespace SaaSAgentSample.Tests.Persistence;

/// <summary>
/// Round-trip tests for the SQLite provider path (arm64 development fallback).
/// Uses <c>EnsureCreated</c> — SQLite has no authoritative migration history
/// per the DB strategy. If this schema drifts from the SQL Server migration,
/// <see cref="Configurations.SubscriptionConfiguration"/> is out of sync and
/// the authoritative migration must be regenerated.
/// </summary>
public class EfSubscriptionRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SaasDbContext> _options;

    public EfSubscriptionRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<SaasDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var seedContext = new SaasDbContext(_options);
        seedContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Add_and_load_persists_state_as_string_name()
    {
        var id = Guid.NewGuid();

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var sub = new Subscription(id, "mkt-42", "offer-x", "plan-basic", Now);
            sub.Activate(Now.AddMinutes(1));

            await repo.AddAsync(sub);
            await repo.SaveChangesAsync();
        }

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var loaded = await repo.GetByIdAsync(id);

            Assert.NotNull(loaded);
            Assert.Equal(SubscriptionState.Subscribed, loaded!.State);
            Assert.Equal("mkt-42", loaded.MarketplaceSubscriptionId);
        }

        // Verify state was persisted as the string name, not the numeric ordinal.
        await using (var db = new SaasDbContext(_options))
        {
            var raw = await db.Database
                .SqlQuery<string>($"SELECT State AS Value FROM Subscriptions WHERE Id = {id}")
                .SingleAsync();
            Assert.Equal("Subscribed", raw);
        }
    }

    [Fact]
    public async Task GetByMarketplaceSubscriptionId_returns_matching_row()
    {
        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            await repo.AddAsync(new Subscription(Guid.NewGuid(), "mkt-lookup", "offer", "plan", Now));
            await repo.SaveChangesAsync();
        }

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var loaded = await repo.GetByMarketplaceSubscriptionIdAsync("mkt-lookup");
            Assert.NotNull(loaded);
        }
    }

    [Fact]
    public async Task Duplicate_marketplace_subscription_id_is_rejected()
    {
        await using var db = new SaasDbContext(_options);
        var repo = new EfSubscriptionRepository(db);

        await repo.AddAsync(new Subscription(Guid.NewGuid(), "dup", "offer", "plan", Now));
        await repo.SaveChangesAsync();

        await repo.AddAsync(new Subscription(Guid.NewGuid(), "dup", "offer", "plan", Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => repo.SaveChangesAsync());
    }

    [Fact]
    public async Task Transitions_persist_across_saves()
    {
        var id = Guid.NewGuid();

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var sub = new Subscription(id, "mkt-lifecycle", "offer", "plan", Now);
            await repo.AddAsync(sub);
            await repo.SaveChangesAsync();
        }

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var sub = await repo.GetByIdAsync(id);
            sub!.Activate(Now.AddMinutes(1));
            sub.Suspend(Now.AddMinutes(2));
            await repo.SaveChangesAsync();
        }

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var sub = await repo.GetByIdAsync(id);
            Assert.Equal(SubscriptionState.Suspended, sub!.State);
        }
    }
}
