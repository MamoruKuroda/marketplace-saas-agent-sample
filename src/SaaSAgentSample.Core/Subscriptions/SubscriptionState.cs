namespace SaaSAgentSample.Core.Subscriptions;

/// <summary>
/// Persistent states of a Microsoft Commercial Marketplace SaaS subscription,
/// as defined by the SaaS fulfillment life cycle (four official states only).
///
/// Reference: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-life-cycle
///
/// Transitions between states are operations (Activate, Suspend, Reinstate,
/// Unsubscribe, ChangePlan) and are not themselves states. See <see cref="Subscription"/>.
/// </summary>
public enum SubscriptionState
{
    /// <summary>
    /// Purchased but not yet activated. Applies only to offers configured for
    /// manual activation. Publisher has 30 days to resolve/activate before the
    /// asset is voided and transitions to <see cref="Unsubscribed"/> (unbilled).
    /// </summary>
    PendingFulfillmentStart = 0,

    /// <summary>
    /// Activated and billed. Reached from PendingFulfillmentStart via Activate,
    /// or directly after purchase when auto-activation is enabled (not used by
    /// this sample).
    /// </summary>
    Subscribed = 1,

    /// <summary>
    /// Temporarily suspended (for example, payment failure). Can be reinstated
    /// back to <see cref="Subscribed"/>, or terminated via Unsubscribe.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Terminal state. Either the customer unsubscribed or the asset was voided
    /// after the 30-day PendingFulfillmentStart timeout. No further transitions.
    /// </summary>
    Unsubscribed = 3,
}
