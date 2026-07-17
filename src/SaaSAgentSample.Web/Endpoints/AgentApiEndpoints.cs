using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Web.Agent;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Web.Endpoints;

/// <summary>
/// The language-agnostic tool boundary: a small JSON operations API over the app's own
/// fulfillment/admin operations, plus a tool-descriptor catalog. An Azure OpenAI tool-calling
/// agent (or, later, a Foundry Agent Service OpenAPI tool) drives these endpoints; the API is
/// described by the OpenAPI document at /openapi/v1.json.
///
/// Guardrails are enforced here, not by the model: reads return authoritative state, the single
/// write requires explicit confirmation, and no endpoint exposes tokens, secrets, or PII.
/// </summary>
public static class AgentApiEndpoints
{
    public static IEndpointRouteBuilder MapAgentApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Agent");

        group.MapGet("/tools", () => Results.Ok(ToolCatalog.All))
            .WithName("ListTools")
            .WithSummary("Lists the agent tool descriptors (function-calling schemas) exposed by this service.");

        group.MapGet("/subscriptions", ListSubscriptionsAsync)
            .WithName("ListSubscriptions")
            .WithSummary("Lists all subscriptions from the authoritative state store.");

        group.MapGet("/subscriptions/{id:guid}", GetSubscriptionAsync)
            .WithName("GetSubscription")
            .WithSummary("Gets a single subscription by its internal id.");

        group.MapPost("/subscriptions/{id:guid}/activate", ActivateSubscriptionAsync)
            .WithName("ActivateSubscription")
            .WithSummary("Activates a subscription. Requires explicit confirmation (confirm=true).");

        return endpoints;
    }

    internal static async Task<IResult> ListSubscriptionsAsync(AdminService admin, CancellationToken cancellationToken)
    {
        var items = await admin.ListSubscriptionsAsync(cancellationToken);
        return Results.Ok(items.Select(SubscriptionDto.FromDomain).ToArray());
    }

    internal static async Task<IResult> GetSubscriptionAsync(Guid id, AdminService admin, CancellationToken cancellationToken)
    {
        var subscription = await admin.GetSubscriptionAsync(id, cancellationToken);
        return subscription is null
            ? Results.NotFound()
            : Results.Ok(SubscriptionDto.FromDomain(subscription));
    }

    internal static async Task<IResult> ActivateSubscriptionAsync(
        Guid id,
        ActivateSubscriptionRequest? request,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        // Guardrail: a state change must be explicitly confirmed. The model cannot activate a
        // subscription without the caller setting confirm=true.
        if (request is null || !request.Confirm)
        {
            return Results.Problem(
                title: "Confirmation required",
                detail: "Activation changes subscription state and requires explicit confirmation (set 'confirm' to true).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await admin.ActivateAsync(id, cancellationToken);
            return result switch
            {
                AdminActivationResult.Activated or AdminActivationResult.AlreadyActive =>
                    Results.Ok(new ActivationResponse(id, result.ToString())),
                AdminActivationResult.NotFound => Results.NotFound(),
                AdminActivationResult.InvalidState =>
                    Results.Conflict(new ActivationResponse(id, result.ToString())),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        catch (FulfillmentApiException)
        {
            return Results.Problem(
                title: "Fulfillment error",
                detail: "The Fulfillment Activate API call failed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    internal sealed record ActivationResponse(Guid Id, string Status);
}
