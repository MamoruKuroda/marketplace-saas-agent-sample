namespace SaaSAgentSample.Data.DependencyInjection;

/// <summary>
/// Supported database providers for the SaaS subscription state store. The
/// value comes from configuration key <c>Database:Provider</c>. SQL Server is
/// the source of truth (authoritative migrations); SQLite is an arm64
/// development fallback via <c>EnsureCreated</c>; InMemory is for tests.
/// </summary>
public enum DatabaseProvider
{
    SqlServer = 0,
    Sqlite = 1,
    InMemory = 2,
}
