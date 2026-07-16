var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "SaaSAgentSample.Web scaffold. Buyer landing, connection webhook, and publisher admin arrive in later PRs. See README.");

app.Run();
