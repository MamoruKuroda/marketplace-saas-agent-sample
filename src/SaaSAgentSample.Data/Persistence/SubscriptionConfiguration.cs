using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSAgentSample.Core.Subscriptions;

namespace SaaSAgentSample.Data.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Subscription"/>. The <c>State</c> column is
/// persisted as its string name so a downstream reader (analytics, ops) never
/// depends on the numeric ordinal of the enum. The schema deliberately fits
/// both SQL Server (authoritative migration) and SQLite (EnsureCreated).
/// </summary>
internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.MarketplaceSubscriptionId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.OfferId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.PlanId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.State)
            .IsRequired()
            .HasMaxLength(64)
            .HasConversion<string>();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.HasIndex(s => s.MarketplaceSubscriptionId)
            .IsUnique()
            .HasDatabaseName("IX_Subscriptions_MarketplaceSubscriptionId");
    }
}
