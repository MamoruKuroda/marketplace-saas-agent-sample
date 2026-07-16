using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaSAgentSample.Fulfillment;
using SaaSAgentSample.Fulfillment.DependencyInjection;
using SaaSAgentSample.Fulfillment.Webhook;

namespace SaaSAgentSample.Tests.FulfillmentTests;

public class FulfillmentDependencyInjectionTests
{
    [Fact]
    public void AddFulfillmentClient_registers_client_and_validator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fulfillment:BaseUrl"] = "http://localhost:3978/api",
                ["Fulfillment:ApiVersion"] = "2018-08-31",
                ["Fulfillment:Webhook:Audience"] = "publisher-app-id",
                ["Fulfillment:Webhook:RequireSignedToken"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFulfillmentClient(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IFulfillmentClient>());
        Assert.NotNull(provider.GetRequiredService<IWebhookTokenValidator>());
        Assert.IsType<DevNullMarketplaceTokenProvider>(provider.GetRequiredService<IMarketplaceTokenProvider>());
    }
}
