namespace SaaSAgentSample.Core.Subscriptions;

/// <summary>
/// Thrown when a subscription state transition is not permitted by the
/// SaaS fulfillment life cycle rules enforced by <see cref="Subscription"/>.
/// </summary>
public sealed class InvalidSubscriptionTransitionException : InvalidOperationException
{
    public InvalidSubscriptionTransitionException(
        SubscriptionState from,
        string operation)
        : base($"Operation '{operation}' is not permitted from state '{from}'.")
    {
        From = from;
        Operation = operation;
    }

    public SubscriptionState From { get; }

    public string Operation { get; }
}
