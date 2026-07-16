using Microsoft.EntityFrameworkCore;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Data.DependencyInjection;
using SaaSAgentSample.Data.Persistence;

namespace SaaSAgentSample.Tests.Persistence;

/// <summary>
/// Integration tests that exercise the authoritative SQL Server schema by
/// applying migrations against a live SQL Server instance. Gated on the
/// <c>SQL_SERVER_CONNECTION</c> environment variable so local runs on arm64
/// (where SQL Server is unavailable) and the default CI lane simply skip.
///
/// The CI SQL Server lane sets <c>SQL_SERVER_CONNECTION</c> to a service
/// container URL following the same <c>docker compose</c> recipe documented in
/// the README, keeping local and CI verification of the authoritative
/// migration identical.
/// </summary>
public class SqlServerIntegrationTests : IAsyncLifetime
{
    private const string ConnectionEnvVar = "SQL_SERVER_CONNECTION";

    private static readonly DateTimeOffset Now = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    private readonly string? _connectionString =
        Environment.GetEnvironmentVariable(ConnectionEnvVar);

    private DbContextOptions<SaasDbContext>? _options;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        _options = new DbContextOptionsBuilder<SaasDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        await using var db = new SaasDbContext(_options);
        db.EnsureSaasSchemaCreated(DatabaseProvider.SqlServer);

        // Reset the table so re-runs are deterministic.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Subscriptions]");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Authoritative_migration_round_trips_subscription()
    {
        if (_options is null)
        {
            // SQL_SERVER_CONNECTION is not set; this lane is intentionally
            // skipped on arm64 dev machines and on the SQLite CI lane.
            return;
        }

        var id = Guid.NewGuid();

        await using (var db = new SaasDbContext(_options))
        {
            var repo = new EfSubscriptionRepository(db);
            var sub = new Subscription(id, $"mkt-{id:N}", "offer", "plan", Now);
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
        }

        await using (var db = new SaasDbContext(_options))
        {
            var raw = await db.Database
                .SqlQuery<string>($"SELECT State AS Value FROM Subscriptions WHERE Id = {id}")
                .SingleAsync();
            Assert.Equal("Subscribed", raw);
        }
    }
}
