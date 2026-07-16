using Microsoft.EntityFrameworkCore;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Data.Persistence.Configurations;

namespace SaaSAgentSample.Data.Persistence;

/// <summary>
/// EF Core context for the SaaS subscription state store. The schema is
/// authoritative for SQL Server (via migrations) and mirrored on SQLite via
/// <c>EnsureCreated</c> for arm64 development.
/// </summary>
public sealed class SaasDbContext : DbContext
{
    public SaasDbContext(DbContextOptions<SaasDbContext> options)
        : base(options)
    {
    }

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
    }
}
