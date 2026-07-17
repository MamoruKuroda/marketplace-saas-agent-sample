using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Tests.WebhookTests;

/// <summary>
/// Fake <see cref="IFulfillmentClient"/> for webhook tests: returns a canned operation from
/// Get Operation (the authorization source of truth) and records Patch Operation calls.
/// </summary>
internal sealed class WebhookFulfillmentClient : IFulfillmentClient
{
    private readonly SubscriptionOperation? _operation;
    private readonly bool _throwOnGetOperation;

    public WebhookFulfillmentClient(SubscriptionOperation? operation, bool throwOnGetOperation = false)
    {
        _operation = operation;
        _throwOnGetOperation = throwOnGetOperation;
    }

    public int PatchCallCount { get; private set; }

    public PatchOperationRequest? LastPatch { get; private set; }

    public string? LastPatchedOperationId { get; private set; }

    public Task<SubscriptionOperation?> GetOperationAsync(string subscriptionId, string operationId, CancellationToken cancellationToken = default)
    {
        if (_throwOnGetOperation)
        {
            throw new InvalidOperationException("Get Operation failed.");
        }

        return Task.FromResult(_operation);
    }

    public Task PatchOperationAsync(string subscriptionId, string operationId, PatchOperationRequest patchRequest, CancellationToken cancellationToken = default)
    {
        PatchCallCount++;
        LastPatch = patchRequest;
        LastPatchedOperationId = operationId;
        return Task.CompletedTask;
    }

    public Task<ResolvedSubscription?> ResolveAsync(string marketplaceToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task ActivateAsync(string subscriptionId, ActivateRequest activateRequest, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<FulfillmentSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<OperationList?> ListOperationsAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
