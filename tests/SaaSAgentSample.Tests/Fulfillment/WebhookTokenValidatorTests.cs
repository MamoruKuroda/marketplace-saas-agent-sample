using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SaaSAgentSample.Fulfillment.Webhook;

namespace SaaSAgentSample.Tests.FulfillmentTests;

public class WebhookTokenValidatorTests
{
    private const string Issuer = "https://login.microsoftonline.com/test/v2.0";
    private const string Audience = "publisher-app-id";
    private static readonly string AppId = WebhookValidationOptions.MarketplaceAppId;

    private static RsaSecurityKey CreateKey() => new(RSA.Create(2048)) { KeyId = Guid.NewGuid().ToString() };

    private static string CreateJwt(
        RsaSecurityKey key,
        string issuer = Issuer,
        string audience = Audience,
        string? appId = null,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["appid"] = appId ?? AppId },
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-5),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(10),
            IssuedAt = DateTime.UtcNow.AddMinutes(-5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        };
        return handler.CreateToken(descriptor);
    }

    private static WebhookTokenValidator CreateValidator(RsaSecurityKey validationKey, WebhookValidationOptions? options = null)
    {
        options ??= new WebhookValidationOptions { Audience = Audience, ValidIssuers = { Issuer } };
        var parameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudiences = new[] { Audience },
            ValidateIssuer = true,
            ValidIssuers = new[] { Issuer },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = validationKey,
        };
        return new WebhookTokenValidator(options, parameters);
    }

    [Fact]
    public async Task Valid_signed_token_passes()
    {
        var key = CreateKey();
        var validator = CreateValidator(key);
        var token = CreateJwt(key);

        var result = await validator.ValidateAsync($"Bearer {token}");

        Assert.True(result.IsValid);
        Assert.Equal(AppId, result.AppId);
    }

    [Fact]
    public async Task Wrong_audience_fails()
    {
        var key = CreateKey();
        var validator = CreateValidator(key);
        var token = CreateJwt(key, audience: "some-other-audience");

        var result = await validator.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Expired_token_fails()
    {
        var key = CreateKey();
        var validator = CreateValidator(key);
        var token = CreateJwt(key, expires: DateTime.UtcNow.AddMinutes(-10), notBefore: DateTime.UtcNow.AddMinutes(-20));

        var result = await validator.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Wrong_signing_key_fails()
    {
        var signingKey = CreateKey();
        var otherKey = CreateKey();
        var validator = CreateValidator(otherKey);
        var token = CreateJwt(signingKey);

        var result = await validator.ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Unexpected_appid_fails()
    {
        var key = CreateKey();
        var validator = CreateValidator(key);
        var token = CreateJwt(key, appId: "11111111-2222-3333-4444-555555555555");

        var result = await validator.ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("Unexpected appid/azp claim.", result.FailureReason);
    }

    [Fact]
    public async Task Lenient_mode_accepts_without_signature_validation()
    {
        var signingKey = CreateKey();
        var options = new WebhookValidationOptions { RequireSignedToken = false };
        // Validation params use a different key; lenient mode must not validate the signature.
        var validator = CreateValidator(CreateKey(), options);
        var token = CreateJwt(signingKey);

        var result = await validator.ValidateAsync(token);

        Assert.True(result.IsValid);
        Assert.Equal(AppId, result.AppId);
    }

    [Fact]
    public async Task Missing_token_fails()
    {
        var validator = CreateValidator(CreateKey());

        var result = await validator.ValidateAsync(null);

        Assert.False(result.IsValid);
    }
}
