namespace SaaSAgentSample.Fulfillment;

/// <summary>
/// Default token provider that returns no token, for use against the token-free
/// Fulfillment API Emulator. Replace with a real Entra client-credentials provider
/// (resource = the Marketplace app) for production.
/// </summary>
public sealed class DevNullMarketplaceTokenProvider : IMarketplaceTokenProvider
{
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<string?>(null);
}
