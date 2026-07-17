using SaaSAgentSample.Web.Agent;

namespace SaaSAgentSample.Tests.AgentTests;

public class ToolCatalogTests
{
    [Fact]
    public void Exposes_the_three_expected_tools()
    {
        var names = ToolCatalog.All.Select(t => t.Name).ToArray();

        Assert.Equal(new[] { "list_subscriptions", "get_subscription", "activate_subscription" }, names);
    }

    [Theory]
    [InlineData("list_subscriptions")]
    [InlineData("get_subscription")]
    public void Read_tools_are_readonly_and_need_no_confirmation(string name)
    {
        var tool = ToolCatalog.All.Single(t => t.Name == name);

        Assert.True(tool.ReadOnly);
        Assert.False(tool.RequiresConfirmation);
    }

    [Fact]
    public void Activate_tool_requires_confirmation_and_is_not_readonly()
    {
        var tool = ToolCatalog.All.Single(t => t.Name == "activate_subscription");

        Assert.True(tool.RequiresConfirmation);
        Assert.False(tool.ReadOnly);
        Assert.Equal("POST", tool.Http.Method);
        Assert.Equal("/api/subscriptions/{id}/activate", tool.Http.PathTemplate);

        // The confirm parameter must be declared and required so a model cannot omit it.
        var required = Assert.IsAssignableFrom<IEnumerable<string>>(tool.Parameters["required"]);
        Assert.Contains("confirm", required);
        var properties = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(tool.Parameters["properties"]);
        Assert.True(properties.ContainsKey("confirm"));
    }

    [Fact]
    public void Every_tool_has_an_object_parameter_schema_and_http_binding()
    {
        foreach (var tool in ToolCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.Equal("object", tool.Parameters["type"]);
            Assert.False(string.IsNullOrWhiteSpace(tool.Http.Method));
            Assert.StartsWith("/api/", tool.Http.PathTemplate);
        }
    }
}
