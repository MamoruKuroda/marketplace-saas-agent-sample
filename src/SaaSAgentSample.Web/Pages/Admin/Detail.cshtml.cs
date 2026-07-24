using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Web.Pages.Admin;

public sealed class DetailModel : PageModel
{
    private readonly AdminService _admin;
    private readonly IStringLocalizer<SharedResource> _l;

    public DetailModel(AdminService admin, IStringLocalizer<SharedResource> l)
    {
        _admin = admin;
        _l = l;
    }

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
                AdminActivationResult.Activated => _l["Subscription activated."],
                AdminActivationResult.AlreadyActive => _l["Subscription was already active."],
                AdminActivationResult.NotFound => _l["Subscription not found."],
                AdminActivationResult.InvalidState => _l["Subscription cannot be activated from its current state."],
                _ => _l["Unknown result."],
            };
        }
        catch (FulfillmentApiException)
        {
            Message = _l["Activation failed while calling the Fulfillment API."];
        }

        Subscription = await _admin.GetSubscriptionAsync(Id, cancellationToken);
        return Subscription is null ? NotFound() : Page();
    }
}
