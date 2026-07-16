using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Fulfillment;

/// <summary>
/// Typed client for the SaaS Fulfillment/Operations API v2. This is a library only —
/// the HTTP endpoints that consume it (buyer landing, connection webhook) arrive in
/// later PRs.
/// </summary>
public interface IFulfillmentClient
{
    /// <summary>Exchanges a marketplace purchase token for the subscription details (Resolve API).</summary>
    Task<ResolvedSubscription?> ResolveAsync(string marketplaceToken, CancellationToken cancellationToken = default);

    /// <summary>Activates a subscription after the account is configured (Activate API). No response body.</summary>
    Task ActivateAsync(string subscriptionId, ActivateRequest activateRequest, CancellationToken cancellationToken = default);

    /// <summary>Gets a subscription's current details (Get Subscription API).</summary>
    Task<FulfillmentSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Lists pending operations for a subscription (List operations API).</summary>
    Task<OperationList?> ListOperationsAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Gets the status of a specific operation (Get operation status API).</summary>
    Task<SubscriptionOperation?> GetOperationAsync(string subscriptionId, string operationId, CancellationToken cancellationToken = default);

    /// <summary>Acknowledges an operation with Success/Failure (Update operation API). No response body.</summary>
    Task PatchOperationAsync(string subscriptionId, string operationId, PatchOperationRequest patchRequest, CancellationToken cancellationToken = default);
}
