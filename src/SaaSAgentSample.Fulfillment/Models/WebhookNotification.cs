namespace SaaSAgentSample.Fulfillment.Models;

/// <summary>
/// Connection webhook notification payload (v2). Deserialization is permissive
/// (unknown fields are ignored) because Microsoft may expand the schema. The purchase
/// token and similarly sensitive fields are intentionally not modeled / not logged.
/// </summary>
public sealed record WebhookNotification
{
    public string? Id { get; init; }
    public string? ActivityId { get; init; }
    public string? SubscriptionId { get; init; }
    public string? PublisherId { get; init; }
    public string? OfferId { get; init; }
    public string? PlanId { get; init; }
    public int? Quantity { get; init; }
    public DateTimeOffset? TimeStamp { get; init; }

    /// <summary>Subscribe | ChangePlan | ChangeQuantity | Renew | Suspend | Unsubscribe | Reinstate (string; permissive).</summary>
    public string? Action { get; init; }

    public string? Status { get; init; }
    public string? OperationRequestSource { get; init; }
    public FulfillmentSubscription? Subscription { get; init; }
}
