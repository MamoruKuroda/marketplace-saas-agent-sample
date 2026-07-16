namespace SaaSAgentSample.Fulfillment;

/// <summary>
/// Configuration for the SaaS Fulfillment API v2 client. Bind from the
/// <c>Fulfillment</c> configuration section.
/// </summary>
public sealed class FulfillmentOptions
{
    public const string SectionName = "Fulfillment";

    /// <summary>
    /// Base URL of the Fulfillment API, up to and including <c>/api</c>.
    /// Production: <c>https://marketplaceapi.microsoft.com/api</c>.
    /// Local: the Fulfillment API Emulator, e.g. <c>http://localhost:3978/api</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://marketplaceapi.microsoft.com/api";

    /// <summary>API version query-string value. v2 uses <c>2018-08-31</c>.</summary>
    public string ApiVersion { get; set; } = "2018-08-31";
}
