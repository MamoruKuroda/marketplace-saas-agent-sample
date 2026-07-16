using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SaaSAgentSample.Fulfillment.Webhook;

/// <summary>
/// Default <see cref="IWebhookTokenValidator"/> using Microsoft.IdentityModel. In production it
/// fully validates the signature/issuer/audience/lifetime (signing keys resolved via the supplied
/// <see cref="TokenValidationParameters"/>, typically from Entra metadata) and checks the
/// appid/azp claim. When <see cref="WebhookValidationOptions.RequireSignedToken"/> is false, it
/// parses the token leniently for use against the token-free local emulator.
/// </summary>
public sealed class WebhookTokenValidator : IWebhookTokenValidator
{
    private readonly WebhookValidationOptions _options;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JsonWebTokenHandler _handler = new();

    public WebhookTokenValidator(WebhookValidationOptions options, TokenValidationParameters validationParameters)
    {
        _options = options;
        _validationParameters = validationParameters;
    }

    public async Task<WebhookTokenValidationResult> ValidateAsync(string? authorizationHeaderValue, CancellationToken cancellationToken = default)
    {
        var token = ExtractToken(authorizationHeaderValue);
        if (string.IsNullOrWhiteSpace(token))
        {
            return WebhookTokenValidationResult.Invalid("Missing Authorization token.");
        }

        if (!_options.RequireSignedToken)
        {
            // The local emulator sends unsigned notifications; parse leniently (dev only).
            try
            {
                var parsed = _handler.ReadJsonWebToken(token);
                return WebhookTokenValidationResult.Valid(GetAppId(parsed));
            }
            catch (Exception ex)
            {
                return WebhookTokenValidationResult.Invalid($"Unparseable token: {ex.GetType().Name}.");
            }
        }

        TokenValidationResult result;
        try
        {
            result = await _handler.ValidateTokenAsync(token, _validationParameters);
        }
        catch (Exception ex)
        {
            return WebhookTokenValidationResult.Invalid($"Validation error: {ex.GetType().Name}.");
        }

        if (!result.IsValid)
        {
            return WebhookTokenValidationResult.Invalid(result.Exception?.GetType().Name ?? "Token validation failed.");
        }

        var appId = GetAppId(result.SecurityToken as JsonWebToken);
        if (!string.IsNullOrEmpty(_options.ExpectedAppId) &&
            !string.Equals(appId, _options.ExpectedAppId, StringComparison.OrdinalIgnoreCase))
        {
            return WebhookTokenValidationResult.Invalid("Unexpected appid/azp claim.");
        }

        return WebhookTokenValidationResult.Valid(appId);
    }

    private static string? ExtractToken(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        return headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? headerValue[bearerPrefix.Length..].Trim()
            : headerValue.Trim();
    }

    private static string? GetAppId(JsonWebToken? token)
    {
        if (token is null)
        {
            return null;
        }

        if (token.TryGetClaim("appid", out var appid))
        {
            return appid.Value;
        }

        return token.TryGetClaim("azp", out var azp) ? azp.Value : null;
    }
}
