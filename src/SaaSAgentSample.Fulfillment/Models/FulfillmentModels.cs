namespace SaaSAgentSample.Fulfillment.Models;

// DTOs for SaaS Fulfillment API v2. Deserialization is intentionally permissive:
// System.Text.Json ignores unknown properties by default, so Microsoft can expand the
// schema without breaking this client. Subscription status / operation action / status
// are kept as strings (not enums) so unknown or expanded values never fail
// deserialization; mapping to the domain SubscriptionState happens in the app layer.

/// <summary>Response of the Resolve API.</summary>
public sealed record ResolvedSubscription
{
    public string? Id { get; init; }
    public string? SubscriptionName { get; init; }
    public string? OfferId { get; init; }
    public string? PlanId { get; init; }
    public int? Quantity { get; init; }
    public FulfillmentSubscription? Subscription { get; init; }
}

/// <summary>SaaS subscription details returned by Resolve / Get Subscription / webhook.</summary>
public sealed record FulfillmentSubscription
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? PublisherId { get; init; }
    public string? OfferId { get; init; }
    public string? PlanId { get; init; }
    public int? Quantity { get; init; }

    /// <summary>PendingFulfillmentStart | Subscribed | Suspended | Unsubscribed (string; mapped to the domain state in the app layer).</summary>
    public string? SaasSubscriptionStatus { get; init; }

    public bool? AutoRenew { get; init; }
    public bool? IsTest { get; init; }
    public bool? IsFreeTrial { get; init; }
    public Term? Term { get; init; }

    // Beneficiary/purchaser carry PII (email, tenant/object IDs). Kept for fidelity to
    // the API, but they MUST NOT be written to logs.
    public Party? Beneficiary { get; init; }
    public Party? Purchaser { get; init; }
}

/// <summary>A beneficiary or purchaser. Contains PII — do not log.</summary>
public sealed record Party
{
    public string? EmailId { get; init; }
    public string? ObjectId { get; init; }
    public string? TenantId { get; init; }
}

public sealed record Term
{
    public string? TermUnit { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
}

/// <summary>An operation returned by the Operations API.</summary>
public sealed record SubscriptionOperation
{
    public string? Id { get; init; }
    public string? ActivityId { get; init; }
    public string? SubscriptionId { get; init; }
    public string? OfferId { get; init; }
    public string? PublisherId { get; init; }
    public string? PlanId { get; init; }
    public int? Quantity { get; init; }

    /// <summary>ChangePlan | ChangeQuantity | Reinstate (string; permissive).</summary>
    public string? Action { get; init; }

    public DateTimeOffset? TimeStamp { get; init; }

    /// <summary>NotStarted | InProgress | Failed | Succeeded | Conflict (string; permissive).</summary>
    public string? Status { get; init; }

    public string? ErrorStatusCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Response of the List outstanding operations API.</summary>
public sealed record OperationList
{
    public IReadOnlyList<SubscriptionOperation> Operations { get; init; } = Array.Empty<SubscriptionOperation>();
}

/// <summary>Body of the Activate Subscription API.</summary>
public sealed record ActivateRequest
{
    public required string PlanId { get; init; }
    public int? Quantity { get; init; }
}

/// <summary>Body of the Update (patch) operation API. <see cref="Status"/> is "Success" or "Failure".</summary>
public sealed record PatchOperationRequest
{
    public required string Status { get; init; }
    public string? PlanId { get; init; }
    public int? Quantity { get; init; }
}
