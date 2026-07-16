using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Data.Persistence;

namespace SaaSAgentSample.Data.DependencyInjection;

/// <summary>
/// Registration entry point for the SaaS subscription state store. Reads
/// <c>Database:Provider</c> (SqlServer | Sqlite | InMemory) and
/// <c>Database:ConnectionString</c> from configuration.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public const string ProviderConfigKey = "Database:Provider";
    public const string ConnectionStringConfigKey = "Database:ConnectionString";
    private const string DefaultInMemoryDatabaseName = "SaasAgentSample";

    public static IServiceCollection AddSaasStateStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var providerName = configuration[ProviderConfigKey] ?? nameof(DatabaseProvider.SqlServer);
        if (!Enum.TryParse<DatabaseProvider>(providerName, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException(
                $"Unsupported '{ProviderConfigKey}' value '{providerName}'. Expected one of: SqlServer, Sqlite, InMemory.");
        }

        var connectionString = configuration[ConnectionStringConfigKey];

        services.AddDbContext<SaasDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                    RequireConnectionString(connectionString, provider);
                    options.UseSqlServer(connectionString);
                    break;

                case DatabaseProvider.Sqlite:
                    RequireConnectionString(connectionString, provider);
                    options.UseSqlite(connectionString);
                    break;

                case DatabaseProvider.InMemory:
                    options.UseInMemoryDatabase(DefaultInMemoryDatabaseName);
                    break;
            }
        });

        services.AddScoped<ISubscriptionRepository, EfSubscriptionRepository>();

        return services;
    }

    /// <summary>
    /// Ensures the database schema is present. SQL Server applies authoritative
    /// migrations (<c>Database.Migrate</c>); SQLite mirrors the model via
    /// <c>EnsureCreated</c> (no migration history is generated for SQLite);
    /// InMemory does nothing.
    /// </summary>
    public static void EnsureSaasSchemaCreated(this SaasDbContext db, DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(db);

        switch (provider)
        {
            case DatabaseProvider.SqlServer:
                db.Database.Migrate();
                break;
            case DatabaseProvider.Sqlite:
                db.Database.EnsureCreated();
                break;
            case DatabaseProvider.InMemory:
                break;
        }
    }

    private static void RequireConnectionString(string? connectionString, DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuration '{ConnectionStringConfigKey}' is required when '{ProviderConfigKey}' is '{provider}'.");
        }
    }
}
