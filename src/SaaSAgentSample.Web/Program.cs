using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using SaaSAgentSample.Data.DependencyInjection;
using SaaSAgentSample.Data.Persistence;
using SaaSAgentSample.Fulfillment.DependencyInjection;
using SaaSAgentSample.Web.Endpoints;
using SaaSAgentSample.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Authoritative subscription state store (PR2) and Fulfillment API client (PR3).
builder.Services.AddSaasStateStore(builder.Configuration);
builder.Services.AddFulfillmentClient(builder.Configuration);
builder.Services.AddScoped<LandingService>();
builder.Services.AddScoped<WebhookService>();
builder.Services.AddScoped<AdminService>();

// Buyer sign-in. In production, require a multitenant Microsoft Entra sign-in (work/school
// + personal accounts; authority "common"). Locally (emulator/dev) set
// Landing:RequireAuthentication=false to skip Entra so the token-free L2 flow can be
// exercised without real sign-in.
var requireAuthentication = builder.Configuration.GetValue("Landing:RequireAuthentication", true);
if (requireAuthentication)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization(options =>
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
}
else
{
    builder.Services.AddAuthorization();
}

builder.Services.AddRazorPages();

// OpenAPI document (/openapi/v1.json) describes the language-agnostic tool boundary so an
// agent runtime can bind to it and it can be promoted to a Foundry OpenAPI tool later.
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure the local schema exists (SQLite dev = EnsureCreated; SQL Server = migrations).
var providerName = builder.Configuration["Database:Provider"] ?? nameof(DatabaseProvider.Sqlite);
if (Enum.TryParse<DatabaseProvider>(providerName, ignoreCase: true, out var databaseProvider))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SaasDbContext>();
    db.EnsureSaasSchemaCreated(databaseProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapConnectionWebhook();
app.MapAgentApi();
app.MapOpenApi();

app.Run();

// Exposed so the integration test project can host the app via WebApplicationFactory<Program>.
public partial class Program;

