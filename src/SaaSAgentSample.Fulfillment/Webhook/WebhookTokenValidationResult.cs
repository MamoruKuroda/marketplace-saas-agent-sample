namespace SaaSAgentSample.Fulfillment.Webhook;

/// <summary>Outcome of validating a webhook Authorization JWT.</summary>
public sealed record WebhookTokenValidationResult
{
    public bool IsValid { get; init; }

    /// <summary>The token's <c>appid</c>/<c>azp</c> claim, when available.</summary>
    public string? AppId { get; init; }

    /// <summary>A short, non-sensitive reason when <see cref="IsValid"/> is false.</summary>
    public string? FailureReason { get; init; }

    public static WebhookTokenValidationResult Valid(string? appId) => new() { IsValid = true, AppId = appId };

    public static WebhookTokenValidationResult Invalid(string reason) => new() { IsValid = false, FailureReason = reason };
}
