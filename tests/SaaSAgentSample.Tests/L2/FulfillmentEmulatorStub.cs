using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SaaSAgentSample.Tests.L2;

/// <summary>
/// A minimal in-repo HTTP stand-in for the Microsoft Commercial Marketplace SaaS Fulfillment
/// API Emulator (microsoft/Commercial-Marketplace-SaaS-API-Emulator). It implements, on a real
/// Kestrel socket, the token-free v2 routes the app's <see cref="SaaSAgentSample.Fulfillment.FulfillmentClient"/>
/// calls, using the emulator's route shape (<c>/api/saas/subscriptions/...</c>). This lets the
/// synthetic L2 test drive the app over the wire exactly as it would against the real emulator,
/// without a container or the emulator's async operation delays.
/// </summary>
internal sealed class FulfillmentEmulatorStub : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, OperationRecord> _operations = new();
    private readonly ConcurrentQueue<PatchRecord> _patches = new();
    private int _activateCount;

    public string BaseUrl { get; private set; } = string.Empty;

    // Synthetic subscription returned by the resolve API.
    public string SubscriptionId { get; init; } = "sub-e2e";
    public string OfferId { get; init; } = "saas-offer";
    public string PlanId { get; init; } = "silver";
    public int Quantity { get; init; } = 1;

    public int ActivateCount => Volatile.Read(ref _activateCount);

    public IReadOnlyList<PatchRecord> Patches => _patches.ToArray();

    private FulfillmentEmulatorStub()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        MapRoutes(_app);
    }

    public static async Task<FulfillmentEmulatorStub> StartAsync()
    {
        var stub = new FulfillmentEmulatorStub();
        await stub._app.StartAsync();

        var addresses = stub._app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;
        stub.BaseUrl = addresses.First();
        return stub;
    }

    /// <summary>Registers an operation so the app's Get Operation authorization check can find it.</summary>
    public void RegisterOperation(string operationId, string subscriptionId, string action, string? planId = null, int? quantity = null)
        => _operations[operationId] = new OperationRecord(operationId, subscriptionId, action, planId, quantity);

    private void MapRoutes(WebApplication app)
    {
        app.MapPost("/api/saas/subscriptions/resolve", () => Results.Json(new
        {
            id = SubscriptionId,
            subscriptionName = "Contoso Test",
            offerId = OfferId,
            planId = PlanId,
            quantity = Quantity,
            subscription = new
            {
                id = SubscriptionId,
                name = "Contoso Test",
                offerId = OfferId,
                planId = PlanId,
                quantity = Quantity,
                saasSubscriptionStatus = "PendingFulfillmentStart",
            },
        }));

        app.MapPost("/api/saas/subscriptions/{id}/activate", (string id) =>
        {
            Interlocked.Increment(ref _activateCount);
            return Results.Ok();
        });

        app.MapGet("/api/saas/subscriptions/{id}", (string id) => Results.Json(new
        {
            id,
            offerId = OfferId,
            planId = PlanId,
            saasSubscriptionStatus = "Subscribed",
        }));

        app.MapGet("/api/saas/subscriptions/{id}/operations/{operationId}", (string id, string operationId) =>
            _operations.TryGetValue(operationId, out var op)
                ? Results.Json(new
                {
                    id = op.OperationId,
                    subscriptionId = op.SubscriptionId,
                    action = op.Action,
                    planId = op.PlanId,
                    quantity = op.Quantity,
                    status = "InProgress",
                })
                : Results.NotFound());

        app.MapMethods("/api/saas/subscriptions/{id}/operations/{operationId}", new[] { "PATCH" }, async (string id, string operationId, HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            var status = JsonDocument.Parse(body).RootElement.GetProperty("status").GetString();
            _patches.Enqueue(new PatchRecord(operationId, status ?? string.Empty));
            return Results.Ok();
        });
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    internal sealed record OperationRecord(string OperationId, string SubscriptionId, string Action, string? PlanId, int? Quantity);

    internal sealed record PatchRecord(string OperationId, string Status);
}
