using System.Text.Json;
using SaaSAgentSample.Fulfillment.Models;
using SaaSAgentSample.Fulfillment.Webhook;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Web.Endpoints;

/// <summary>
/// The Microsoft-facing connection webhook endpoint. Microsoft POSTs subscription lifecycle
/// notifications here. The endpoint performs the first server-side check (Entra JWT validation)
/// and hands the payload to <see cref="WebhookService"/> for the second check (Get Operation
/// authorization) and dispatch. It is anonymous to the app's user sign-in because it is
/// authenticated by the Marketplace JWT, not by an interactive user.
///
/// Reference: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook
/// </summary>
public static class WebhookEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IEndpointRouteBuilder MapConnectionWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/webhook", HandleAsync).AllowAnonymous();
        return endpoints;
    }

    internal static async Task<IResult> HandleAsync(
        HttpRequest request,
        IWebhookTokenValidator tokenValidator,
        WebhookService webhookService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(WebhookEndpoint));

        // Stage one: validate the Microsoft Entra JWT from the Authorization header (server-side).
        var authorization = request.Headers.Authorization.ToString();
        var tokenResult = await tokenValidator.ValidateAsync(authorization, cancellationToken);
        if (!tokenResult.IsValid)
        {
            logger.LogWarning("Rejected webhook: token validation failed ({Reason}).", tokenResult.FailureReason);
            return Results.Unauthorized();
        }

        // Permissive deserialization: unknown fields are ignored so Microsoft can expand the schema.
        WebhookNotification? notification;
        try
        {
            notification = await request.ReadFromJsonAsync<WebhookNotification>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Rejected webhook: malformed payload ({ErrorType}).", ex.GetType().Name);
            return Results.BadRequest();
        }

        if (notification is null)
        {
            logger.LogWarning("Rejected webhook: empty payload.");
            return Results.BadRequest();
        }

        var result = await webhookService.ProcessAsync(notification, cancellationToken);
        return result switch
        {
            // Acknowledge receipt with 200 as soon as the call is processed (per Microsoft docs).
            WebhookProcessingResult.Succeeded or WebhookProcessingResult.Ignored => Results.Ok(),
            WebhookProcessingResult.NotAuthorized => Results.StatusCode(StatusCodes.Status403Forbidden),
            WebhookProcessingResult.SubscriptionNotFound => Results.NotFound(),
            WebhookProcessingResult.Conflict => Results.Conflict(),
            WebhookProcessingResult.Invalid => Results.BadRequest(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
