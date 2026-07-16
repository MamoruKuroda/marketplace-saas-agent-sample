using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SaaSAgentSample.Data.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools to build a
/// <see cref="SaasDbContext"/> targeting SQL Server. This factory is only
/// consulted by <c>dotnet ef migrations add</c> / <c>dotnet ef dbcontext</c>
/// and is intentionally locked to the SQL Server provider so migrations
/// stay authoritative for SQL Server, per the DB strategy documented in
/// the README.
///
/// The connection string is a placeholder — migrations are compiled, not
/// executed, at design time.
/// </summary>
internal sealed class SaasDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SaasDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SaasAgentSampleDesignTime;Trusted_Connection=True;MultipleActiveResultSets=true";

    public SaasDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SaasDbContext>()
            .UseSqlServer(
                DesignTimeConnectionString,
                sql => sql.MigrationsAssembly(typeof(SaasDbContext).Assembly.FullName))
            .Options;

        return new SaasDbContext(options);
    }
}
