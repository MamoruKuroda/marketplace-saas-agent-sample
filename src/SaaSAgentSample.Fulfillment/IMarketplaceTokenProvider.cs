namespace SaaSAgentSample.Fulfillment;

/// <summary>
/// Supplies the OAuth bearer token used to authorize calls to the Fulfillment API
/// (resource = the well-known Marketplace application). Abstracted so the token-free
/// Fulfillment API Emulator can be driven with a no-op provider locally, while
/// production supplies a real Microsoft Entra client-credentials token.
/// </summary>
public interface IMarketplaceTokenProvider
{
    /// <summary>
    /// Returns the bearer token value (without the "Bearer " prefix), or <c>null</c>
    /// to send no Authorization header (token-free emulator / dev).
    /// </summary>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
