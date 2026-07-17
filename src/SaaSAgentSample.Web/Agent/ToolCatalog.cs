namespace SaaSAgentSample.Web.Agent;

/// <summary>HTTP binding for a tool, so any agent runtime can map a tool call to this app's endpoint.</summary>
public sealed record ToolHttpBinding
{
    public required string Method { get; init; }
    public required string PathTemplate { get; init; }
}

/// <summary>
/// A language-agnostic tool descriptor. The shape mirrors the OpenAI/Foundry function-calling
/// contract (name + description + JSON-Schema parameters) so it can be handed directly to an
/// Azure OpenAI tool-calling agent now, and promoted to a Foundry Agent Service OpenAPI tool
/// later without rewriting the boundary.
/// </summary>
public sealed record ToolDescriptor
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>JSON Schema describing the tool's parameters.</summary>
    public required IReadOnlyDictionary<string, object?> Parameters { get; init; }

    /// <summary>How this tool maps onto the app's HTTP operations API.</summary>
    public required ToolHttpBinding Http { get; init; }

    /// <summary>True when the tool only reads authoritative state (no side effects).</summary>
    public bool ReadOnly { get; init; }

    /// <summary>True when the tool changes state and therefore requires explicit human confirmation.</summary>
    public bool RequiresConfirmation { get; init; }
}

/// <summary>
/// The set of tools this service exposes to an agent. Read tools return authoritative state; the
/// single write tool (activate) requires explicit confirmation. Nothing here lets a model bypass
/// the guardrails: webhook authorization is never exposed as a tool, and no tool returns tokens,
/// secrets, or PII.
/// </summary>
public static class ToolCatalog
{
    private static IReadOnlyDictionary<string, object?> Object(
        IReadOnlyDictionary<string, object?> properties,
        params string[] required) => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };

    private static Dictionary<string, object?> String(string description, string? format = null)
    {
        var schema = new Dictionary<string, object?> { ["type"] = "string", ["description"] = description };
        if (format is not null)
        {
            schema["format"] = format;
        }

        return schema;
    }

    public static IReadOnlyList<ToolDescriptor> All { get; } = new List<ToolDescriptor>
    {
        new()
        {
            Name = "list_subscriptions",
            Description = "Lists all SaaS subscriptions from the authoritative state store, most recently created first.",
            ReadOnly = true,
            Parameters = Object(new Dictionary<string, object?>()),
            Http = new ToolHttpBinding { Method = "GET", PathTemplate = "/api/subscriptions" },
        },
        new()
        {
            Name = "get_subscription",
            Description = "Gets a single subscription by its internal id from the authoritative state store.",
            ReadOnly = true,
            Parameters = Object(
                new Dictionary<string, object?>
                {
                    ["id"] = String("The subscription's internal id (GUID).", format: "uuid"),
                },
                "id"),
            Http = new ToolHttpBinding { Method = "GET", PathTemplate = "/api/subscriptions/{id}" },
        },
        new()
        {
            Name = "activate_subscription",
            Description = "Activates a subscription that is awaiting fulfillment. This changes billing state and "
                + "requires explicit human confirmation: 'confirm' must be true. The service validates the "
                + "transition and never fabricates entitlement.",
            RequiresConfirmation = true,
            Parameters = Object(
                new Dictionary<string, object?>
                {
                    ["id"] = String("The subscription's internal id (GUID).", format: "uuid"),
                    ["confirm"] = new Dictionary<string, object?>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Must be true. Explicit confirmation is required before any state change.",
                    },
                },
                "id",
                "confirm"),
            Http = new ToolHttpBinding { Method = "POST", PathTemplate = "/api/subscriptions/{id}/activate" },
        },
    };
}
