using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SaaSAgentSample.Fulfillment.Webhook;

namespace SaaSAgentSample.Fulfillment.DependencyInjection;

/// <summary>Registers the Fulfillment API client and the webhook token validator.</summary>
public static class FulfillmentServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FulfillmentOptions>(configuration.GetSection(FulfillmentOptions.SectionName));

        // Default token provider is token-free (emulator/dev). Apps replace it with a real
        // Entra client-credentials provider (resource = the Marketplace app) for production.
        services.TryAddSingleton<IMarketplaceTokenProvider, DevNullMarketplaceTokenProvider>();

        services.AddHttpClient<IFulfillmentClient, FulfillmentClient>();

        var webhookOptions = new WebhookValidationOptions();
        configuration.GetSection(WebhookValidationOptions.SectionName).Bind(webhookOptions);
        services.TryAddSingleton(webhookOptions);
        services.TryAddSingleton<IWebhookTokenValidator>(_ =>
            new WebhookTokenValidator(webhookOptions, BuildValidationParameters(webhookOptions)));

        return services;
    }

    /// <summary>
    /// Builds token validation parameters from options, resolving Entra signing keys from
    /// OpenID metadata when a metadata address is configured.
    /// </summary>
    public static TokenValidationParameters BuildValidationParameters(WebhookValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrEmpty(options.Audience),
            ValidAudiences = string.IsNullOrEmpty(options.Audience) ? null : new[] { options.Audience! },
            ValidateIssuer = options.ValidIssuers.Count > 0,
            ValidIssuers = options.ValidIssuers,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };

        if (!string.IsNullOrEmpty(options.MetadataAddress))
        {
            parameters.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                options.MetadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
        }

        return parameters;
    }
}
