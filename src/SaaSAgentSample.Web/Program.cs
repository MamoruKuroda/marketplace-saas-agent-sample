using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
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

// Localization (en default, ja). Culture is chosen per request from the query string
// (?culture=), then a cookie (set by the header language toggle), then the browser's
// Accept-Language header. Resources live in Resources/SharedResource.<culture>.resx.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ja") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Emit non-Latin text (e.g. Japanese) as literal UTF-8 rather than numeric HTML entities.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

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

app.UseRequestLocalization();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapConnectionWebhook();

// Persist the buyer/publisher UI language when the header toggle is used, then return
// to the page they were on. Only the supported cultures are honored.
app.MapGet("/set-culture", (string culture, string? redirect, HttpContext http) =>
{
    if (culture is "en" or "ja")
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, Path = "/" });
    }

    return Results.LocalRedirect(string.IsNullOrEmpty(redirect) ? "/" : redirect);
});

app.Run();

// Exposed so the integration test project can host the app via WebApplicationFactory<Program>.
public partial class Program;

