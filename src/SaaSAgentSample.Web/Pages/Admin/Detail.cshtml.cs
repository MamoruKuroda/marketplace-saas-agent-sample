using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Web.Pages.Admin;

public sealed class DetailModel : PageModel
{
    private readonly AdminService _admin;

    public DetailModel(AdminService admin) => _admin = admin;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Subscription? Subscription { get; private set; }

    public string? Message { get; private set; }

    /// <summary>Activation is only offered from the PendingFulfillmentStart state.</summary>
    public bool CanActivate => Subscription?.State == SubscriptionState.PendingFulfillmentStart;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Subscription = await _admin.GetSubscriptionAsync(Id, cancellationToken);
        return Subscription is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _admin.ActivateAsync(Id, cancellationToken);
            Message = result switch
            {
                AdminActivationResult.Activated => "Subscription activated.",
                AdminActivationResult.AlreadyActive => "Subscription was already active.",
                AdminActivationResult.NotFound => "Subscription not found.",
                AdminActivationResult.InvalidState => "Subscription cannot be activated from its current state.",
                _ => "Unknown result.",
            };
        }
        catch (FulfillmentApiException)
        {
            Message = "Activation failed while calling the Fulfillment API.";
        }

        Subscription = await _admin.GetSubscriptionAsync(Id, cancellationToken);
        return Subscription is null ? NotFound() : Page();
    }
}
