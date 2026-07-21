using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Web.Services;

/// <summary>Outcome of processing a connection webhook notification, mapped to HTTP by the endpoint.</summary>
public enum WebhookProcessingResult
{
    /// <summary>Verified and applied (or an intentional informational no-op). Acknowledge with HTTP 200.</summary>
    Succeeded,

    /// <summary>The Get Operation authorization check failed; the payload was not acted upon.</summary>
    NotAuthorized,

    /// <summary>No local subscription record exists for the notification's subscription id.</summary>
    SubscriptionNotFound,

    /// <summary>The requested transition is not valid from the current state.</summary>
    Conflict,

    /// <summary>An unknown/unmapped action was acknowledged without taking action.</summary>
    Ignored,

    /// <summary>The payload was missing the identifiers required to process it.</summary>
    Invalid,
}

/// <summary>
/// Processes SaaS connection webhook notifications. Implements the second, server-side
/// authorization stage required by Microsoft: after the Entra JWT is validated (stage one,
/// <see cref="SaaSAgentSample.Fulfillment.Webhook.IWebhookTokenValidator"/>), the payload is
/// authorized against Microsoft's truth via the Get Operation API before the authoritative
/// state store is mutated. Both validation stages run entirely in server-side code.
///
/// Reference: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook
/// </summary>
public sealed class WebhookService
{
    private readonly IFulfillmentClient _fulfillment;
    private readonly ISubscriptionRepository _repository;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(IFulfillmentClient fulfillment, ISubscriptionRepository repository, ILogger<WebhookService> logger)
    {
        _fulfillment = fulfillment;
        _repository = repository;
        _logger = logger;
    }

    public async Task<WebhookProcessingResult> ProcessAsync(WebhookNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var subscriptionId = notification.SubscriptionId;
        var operationId = notification.Id;
        var action = notification.Action;

        if (string.IsNullOrWhiteSpace(subscriptionId) ||
            string.IsNullOrWhiteSpace(operationId) ||
            string.IsNullOrWhiteSpace(action))
        {
            _logger.LogWarning("Webhook payload missing subscriptionId/operationId/action; ignoring.");
            return WebhookProcessingResult.Invalid;
        }

        // Stage two: authorize the payload against Microsoft via the Get Operation API before
        // mutating any state. A forged or stale payload will not match a real operation.
        SubscriptionOperation? operation;
        try
        {
            operation = await _fulfillment.GetOperationAsync(subscriptionId, operationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Get Operation failed for subscription {SubscriptionId}: {ErrorType}.", subscriptionId, ex.GetType().Name);
            return WebhookProcessingResult.NotAuthorized;
        }

        if (operation is null ||
            !string.Equals(operation.SubscriptionId, subscriptionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(operation.Action, action, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Webhook payload not authorized by Get Operation for subscription {SubscriptionId}, action {Action}.", subscriptionId, action);
            return WebhookProcessingResult.NotAuthorized;
        }

        var subscription = await _repository.GetByMarketplaceSubscriptionIdAsync(subscriptionId, cancellationToken);
        if (subscription is null)
        {
            _logger.LogWarning("No local subscription for {SubscriptionId}; cannot apply {Action}.", subscriptionId, action);
            return WebhookProcessingResult.SubscriptionNotFound;
        }

        var now = DateTimeOffset.UtcNow;
        var acknowledge = false;

        try
        {
            switch (action.ToLowerInvariant())
            {
                case "suspend":
                    if (subscription.State != SubscriptionState.Suspended)
                    {
                        subscription.Suspend(now);
                    }

                    break;

                case "unsubscribe":
                    if (subscription.State != SubscriptionState.Unsubscribed)
                    {
                        subscription.Unsubscribe(now);
                    }

                    break;

                case "reinstate":
                    if (subscription.State != SubscriptionState.Subscribed)
                    {
                        subscription.Reinstate(now);
                    }

                    break;

                case "changeplan":
                    var newPlanId = operation.PlanId ?? notification.PlanId;
                    if (string.IsNullOrWhiteSpace(newPlanId))
                    {
                        return WebhookProcessingResult.Invalid;
                    }

                    var alreadyOnPlan = subscription.State == SubscriptionState.Subscribed &&
                        string.Equals(subscription.PlanId, newPlanId, StringComparison.Ordinal);
                    if (!alreadyOnPlan)
                    {
                        subscription.ChangePlan(newPlanId, now);
                    }

                    acknowledge = true;
                    break;

                case "changequantity":
                    // The v0 domain state has no quantity dimension, so there is nothing to mutate;
                    // still acknowledge the operation so Microsoft does not auto-accept by timeout.
                    acknowledge = true;
                    break;

                case "subscribe":
                case "renew":
                    // Informational only. Activation is driven by the buyer landing (Activate); renewal
                    // does not change the 4-state model. No state mutation.
                    _logger.LogInformation("Received informational {Action} for {SubscriptionId}; no state change.", action, subscriptionId);
                    return WebhookProcessingResult.Succeeded;

                default:
                    _logger.LogWarning("Unknown webhook action {Action} for {SubscriptionId}; acknowledging without action.", action, subscriptionId);
                    return WebhookProcessingResult.Ignored;
            }
        }
        catch (InvalidSubscriptionTransitionException)
        {
            _logger.LogWarning("Invalid transition applying {Action} for {SubscriptionId}.", action, subscriptionId);
            return WebhookProcessingResult.Conflict;
        }

        await _repository.SaveChangesAsync(cancellationToken);

        if (acknowledge)
        {
            await AcknowledgeAsync(subscriptionId, operationId, operation.PlanId, operation.Quantity, cancellationToken);
        }

        _logger.LogInformation("Processed {Action} for {SubscriptionId}.", action, subscriptionId);
        return WebhookProcessingResult.Succeeded;
    }

    /// <summary>
    /// Acknowledges an operation with the Update (patch) operation API. Only ChangePlan and
    /// ChangeQuantity require this; Microsoft auto-accepts if there is no reply within 10 seconds,
    /// so a failure here is logged but not fatal (our state is already authoritative).
    /// </summary>
    private async Task AcknowledgeAsync(string subscriptionId, string operationId, string? planId, int? quantity, CancellationToken cancellationToken)
    {
        try
        {
            await _fulfillment.PatchOperationAsync(
                subscriptionId,
                operationId,
                new PatchOperationRequest { Status = "Success", PlanId = planId, Quantity = quantity },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Patch Operation acknowledgement failed for {SubscriptionId}: {ErrorType}.", subscriptionId, ex.GetType().Name);
        }
    }
}
