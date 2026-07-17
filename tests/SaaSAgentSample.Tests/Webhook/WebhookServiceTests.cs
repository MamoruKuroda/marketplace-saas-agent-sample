using Microsoft.Extensions.Logging.Abstractions;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment.Models;
using SaaSAgentSample.Tests.LandingTests;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Tests.WebhookTests;

public class WebhookServiceTests
{
    private const string SubId = "sub-guid-1";
    private const string OpId = "op-guid-1";

    private static Subscription Subscription(SubscriptionState target, string planId = "silver")
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = new Subscription(Guid.NewGuid(), SubId, "offer1", planId, now);
        switch (target)
        {
            case SubscriptionState.PendingFulfillmentStart:
                break;
            case SubscriptionState.Subscribed:
                subscription.Activate(now);
                break;
            case SubscriptionState.Suspended:
                subscription.Activate(now);
                subscription.Suspend(now);
                break;
            case SubscriptionState.Unsubscribed:
                subscription.Unsubscribe(now);
                break;
        }

        return subscription;
    }

    private static SubscriptionOperation Operation(string action, string? planId = null, int? quantity = null) => new()
    {
        Id = OpId,
        SubscriptionId = SubId,
        Action = action,
        PlanId = planId,
        Quantity = quantity,
        Status = "InProgress",
    };

    private static WebhookNotification Notification(string action, string? planId = null, int? quantity = null, string? subscriptionId = SubId, string? operationId = OpId) => new()
    {
        Id = operationId,
        SubscriptionId = subscriptionId,
        Action = action,
        PlanId = planId,
        Quantity = quantity,
    };

    private static (WebhookService Service, InMemorySubscriptionRepository Repository, WebhookFulfillmentClient Fulfillment) Build(
        SubscriptionOperation? operation,
        Subscription? seed,
        bool throwOnGetOperation = false)
    {
        var repository = new InMemorySubscriptionRepository();
        if (seed is not null)
        {
            repository.AddAsync(seed).GetAwaiter().GetResult();
        }

        var fulfillment = new WebhookFulfillmentClient(operation, throwOnGetOperation);
        var service = new WebhookService(fulfillment, repository, NullLogger<WebhookService>.Instance);
        return (service, repository, fulfillment);
    }

    [Fact]
    public async Task Suspend_transitions_subscribed_to_suspended()
    {
        var (service, repository, fulfillment) = Build(Operation("Suspend"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Suspend"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Suspended, repository.Items[0].State);
        Assert.Equal(0, fulfillment.PatchCallCount); // Suspend requires no ack
    }

    [Fact]
    public async Task Unsubscribe_transitions_to_unsubscribed()
    {
        var (service, repository, _) = Build(Operation("Unsubscribe"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Unsubscribe"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Unsubscribed, repository.Items[0].State);
    }

    [Fact]
    public async Task Reinstate_transitions_suspended_to_subscribed()
    {
        var (service, repository, _) = Build(Operation("Reinstate"), Subscription(SubscriptionState.Suspended));

        var result = await service.ProcessAsync(Notification("Reinstate"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
    }

    [Fact]
    public async Task ChangePlan_updates_plan_and_acknowledges()
    {
        var (service, repository, fulfillment) = Build(
            Operation("ChangePlan", planId: "gold"),
            Subscription(SubscriptionState.Subscribed, planId: "silver"));

        var result = await service.ProcessAsync(Notification("ChangePlan", planId: "gold"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal("gold", repository.Items[0].PlanId);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
        Assert.Equal(1, fulfillment.PatchCallCount);
        Assert.Equal("Success", fulfillment.LastPatch!.Status);
        Assert.Equal(OpId, fulfillment.LastPatchedOperationId);
    }

    [Fact]
    public async Task ChangeQuantity_acknowledges_without_state_change()
    {
        var (service, repository, fulfillment) = Build(
            Operation("ChangeQuantity", quantity: 20),
            Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("ChangeQuantity", quantity: 20));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
        Assert.Equal(1, fulfillment.PatchCallCount);
        Assert.Equal("Success", fulfillment.LastPatch!.Status);
    }

    [Fact]
    public async Task Suspend_is_idempotent_when_already_suspended()
    {
        var (service, repository, _) = Build(Operation("Suspend"), Subscription(SubscriptionState.Suspended));

        var result = await service.ProcessAsync(Notification("Suspend"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Suspended, repository.Items[0].State);
    }

    [Fact]
    public async Task Subscribe_is_informational_noop()
    {
        var (service, repository, fulfillment) = Build(Operation("Subscribe"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Subscribe"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
        Assert.Equal(0, fulfillment.PatchCallCount);
    }

    [Fact]
    public async Task NotAuthorized_when_get_operation_returns_null()
    {
        var (service, repository, _) = Build(operation: null, Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Suspend"));

        Assert.Equal(WebhookProcessingResult.NotAuthorized, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State); // unchanged
    }

    [Fact]
    public async Task NotAuthorized_when_get_operation_throws()
    {
        var (service, _, _) = Build(Operation("Suspend"), Subscription(SubscriptionState.Subscribed), throwOnGetOperation: true);

        var result = await service.ProcessAsync(Notification("Suspend"));

        Assert.Equal(WebhookProcessingResult.NotAuthorized, result);
    }

    [Fact]
    public async Task NotAuthorized_when_operation_action_mismatches_payload()
    {
        // Get Operation says Suspend, but the payload claims Unsubscribe -> not authorized.
        var (service, repository, _) = Build(Operation("Suspend"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Unsubscribe"));

        Assert.Equal(WebhookProcessingResult.NotAuthorized, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
    }

    [Fact]
    public async Task SubscriptionNotFound_when_no_local_record()
    {
        var (service, _, _) = Build(Operation("Suspend"), seed: null);

        var result = await service.ProcessAsync(Notification("Suspend"));

        Assert.Equal(WebhookProcessingResult.SubscriptionNotFound, result);
    }

    [Fact]
    public async Task Conflict_when_transition_is_invalid()
    {
        // ChangePlan requires Subscribed; applying it to a Suspended record is invalid.
        var (service, repository, fulfillment) = Build(
            Operation("ChangePlan", planId: "gold"),
            Subscription(SubscriptionState.Suspended, planId: "silver"));

        var result = await service.ProcessAsync(Notification("ChangePlan", planId: "gold"));

        Assert.Equal(WebhookProcessingResult.Conflict, result);
        Assert.Equal(SubscriptionState.Suspended, repository.Items[0].State);
        Assert.Equal("silver", repository.Items[0].PlanId); // unchanged
        Assert.Equal(0, fulfillment.PatchCallCount); // no ack on conflict
    }

    [Fact]
    public async Task Reinstate_is_idempotent_when_already_subscribed()
    {
        var (service, repository, _) = Build(Operation("Reinstate"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Reinstate"));

        Assert.Equal(WebhookProcessingResult.Succeeded, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
    }

    [Fact]
    public async Task Unknown_action_is_ignored()
    {
        var (service, repository, fulfillment) = Build(Operation("Frobnicate"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Frobnicate"));

        Assert.Equal(WebhookProcessingResult.Ignored, result);
        Assert.Equal(SubscriptionState.Subscribed, repository.Items[0].State);
        Assert.Equal(0, fulfillment.PatchCallCount);
    }

    [Fact]
    public async Task Invalid_when_identifiers_missing()
    {
        var (service, _, _) = Build(Operation("Suspend"), Subscription(SubscriptionState.Subscribed));

        var result = await service.ProcessAsync(Notification("Suspend", subscriptionId: null));

        Assert.Equal(WebhookProcessingResult.Invalid, result);
    }
}
