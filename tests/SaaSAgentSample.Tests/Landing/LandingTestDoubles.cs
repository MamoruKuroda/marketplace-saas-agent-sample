using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Tests.LandingTests;

/// <summary>In-memory <see cref="ISubscriptionRepository"/> for unit tests.</summary>
internal sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly List<Subscription> _items = new();

    public IReadOnlyList<Subscription> Items => _items;

    public int SaveCount { get; private set; }

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(s => s.Id == id));

    public Task<Subscription?> GetByMarketplaceSubscriptionIdAsync(string marketplaceSubscriptionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(s => s.MarketplaceSubscriptionId == marketplaceSubscriptionId));

    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        _items.Add(subscription);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Fake <see cref="IFulfillmentClient"/> that records Activate calls and returns a canned Resolve result.</summary>
internal sealed class FakeFulfillmentClient : IFulfillmentClient
{
    private readonly ResolvedSubscription? _resolveResult;

    public FakeFulfillmentClient(ResolvedSubscription? resolveResult = null) => _resolveResult = resolveResult;

    public int ActivateCallCount { get; private set; }

    public string? LastActivatedSubscriptionId { get; private set; }

    public Task<ResolvedSubscription?> ResolveAsync(string marketplaceToken, CancellationToken cancellationToken = default)
        => Task.FromResult(_resolveResult);

    public Task ActivateAsync(string subscriptionId, ActivateRequest activateRequest, CancellationToken cancellationToken = default)
    {
        ActivateCallCount++;
        LastActivatedSubscriptionId = subscriptionId;
        return Task.CompletedTask;
    }

    public Task<FulfillmentSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<OperationList?> ListOperationsAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<SubscriptionOperation?> GetOperationAsync(string subscriptionId, string operationId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task PatchOperationAsync(string subscriptionId, string operationId, PatchOperationRequest patchRequest, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
