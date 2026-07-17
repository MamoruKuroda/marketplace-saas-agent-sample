using Microsoft.AspNetCore.Mvc.RazorPages;
using SaaSAgentSample.Core.Subscriptions;
using SaaSAgentSample.Web.Services;

namespace SaaSAgentSample.Web.Pages.Admin;

public sealed class IndexModel : PageModel
{
    private readonly AdminService _admin;

    public IndexModel(AdminService admin) => _admin = admin;

    public IReadOnlyList<Subscription> Subscriptions { get; private set; } = Array.Empty<Subscription>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Subscriptions = await _admin.ListSubscriptionsAsync(cancellationToken);
}
