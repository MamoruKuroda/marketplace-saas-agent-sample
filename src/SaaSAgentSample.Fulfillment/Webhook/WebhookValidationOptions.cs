namespace SaaSAgentSample.Fulfillment.Webhook;

/// <summary>
/// Options controlling validation of the Microsoft Entra JWT that Microsoft attaches
/// (Authorization header) to connection webhook calls. Server-side validation only —
/// never delegated to an LLM.
/// </summary>
public sealed class WebhookValidationOptions
{
    public const string SectionName = "Fulfillment:Webhook";

    /// <summary>
    /// Well-known, public Microsoft Marketplace application ID that signs webhook calls.
    /// This is a documented public constant, not a secret.
    /// </summary>
    public const string MarketplaceAppId = "20e940b3-4c07-4bc1-a733-45f7c7a3d0e3";

    /// <summary>Expected token audience — the publisher's Microsoft Entra application (client) ID.</summary>
    public string? Audience { get; set; }

    /// <summary>Accepted token issuers (Microsoft Entra). If empty, issuer validation is skipped.</summary>
    public IList<string> ValidIssuers { get; set; } = new List<string>();

    /// <summary>The <c>appid</c>/<c>azp</c> claim must equal this. Defaults to the Marketplace app ID.</summary>
    public string ExpectedAppId { get; set; } = MarketplaceAppId;

    /// <summary>
    /// When true (default, production), the token signature/issuer/audience/lifetime are
    /// fully validated. When false (local emulator, which sends unsigned notifications),
    /// the token is parsed leniently without signature validation. Never set false in production.
    /// </summary>
    public bool RequireSignedToken { get; set; } = true;

    /// <summary>Microsoft Entra OpenID metadata address used to fetch signing keys in production.</summary>
    public string? MetadataAddress { get; set; }
}
