namespace SaaSAgentSample.Fulfillment.Webhook;

/// <summary>
/// Validates the Microsoft Entra JWT presented in a webhook call's Authorization header.
/// This is the first of the two server-side webhook checks (the second being a call to the
/// Get Operation API to authorize the payload before acting). Validation is done entirely
/// server-side and is never delegated to an LLM.
/// </summary>
public interface IWebhookTokenValidator
{
    /// <param name="authorizationHeaderValue">The Authorization header value ("Bearer &lt;jwt&gt;") or a raw JWT.</param>
    Task<WebhookTokenValidationResult> ValidateAsync(string? authorizationHeaderValue, CancellationToken cancellationToken = default);
}
