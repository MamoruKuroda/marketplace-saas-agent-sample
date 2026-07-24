using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.Models;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Web.Pages;

public sealed class IndexModel : PageModel
{
    private readonly LandingService _landing;
    private readonly IStringLocalizer<SharedResource> _l;

    public IndexModel(LandingService landing, IStringLocalizer<SharedResource> l)
    {
        _landing = landing;
        _l = l;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public ResolvedSubscription? Resolved { get; private set; }

    public string? Message { get; private set; }

    public bool IsActivated { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            Message = _l["No purchase token was provided. Open this page from the Marketplace 'Configure account' link."];
            return;
        }

        try
        {
            Resolved = await _landing.ResolveAsync(Token, cancellationToken);
            if (Resolved is null)
            {
                Message = _l["The purchase could not be resolved."];
            }
        }
        catch (FulfillmentApiException)
        {
            Message = _l["The purchase could not be resolved (the token may be invalid or expired)."];
        }
    }

    public async Task<IActionResult> OnPostAsync(string subscriptionId, string planId, int? quantity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(planId))
        {
            Message = _l["Missing subscription details."];
            return Page();
        }

        try
        {
            var result = await _landing.ActivateAsync(subscriptionId, planId, quantity, cancellationToken);
            IsActivated = result == LandingActivationResult.Activated;
            Message = IsActivated
                ? _l["Your subscription is now active."]
                : _l["Activation could not be completed. Please retry from the Marketplace."];
        }
        catch (FulfillmentApiException)
        {
            Message = _l["Activation failed. Please try again."];
        }

        return Page();
    }
}
