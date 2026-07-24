using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Fulfillment;

/// <summary>Typed client for the SaaS Fulfillment/Operations API v2.</summary>
public sealed class FulfillmentClient : IFulfillmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly IMarketplaceTokenProvider _tokenProvider;
    private readonly FulfillmentOptions _options;

    public FulfillmentClient(HttpClient httpClient, IMarketplaceTokenProvider tokenProvider, IOptions<FulfillmentOptions> options)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    public Task<ResolvedSubscription?> ResolveAsync(string marketplaceToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceToken);
        var request = CreateRequest(HttpMethod.Post, "subscriptions/resolve");
        request.Headers.TryAddWithoutValidation("x-ms-marketplace-token", marketplaceToken);
        return SendForJsonAsync<ResolvedSubscription>(request, nameof(ResolveAsync), cancellationToken);
    }

    public Task ActivateAsync(string subscriptionId, ActivateRequest activateRequest, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(activateRequest);
        var request = CreateRequest(HttpMethod.Post, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/activate");
        request.Content = JsonContent.Create(activateRequest, options: JsonOptions);
        return SendAsync(request, nameof(ActivateAsync), cancellationToken);
    }

    public Task<FulfillmentSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        var request = CreateRequest(HttpMethod.Get, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}");
        return SendForJsonAsync<FulfillmentSubscription>(request, nameof(GetSubscriptionAsync), cancellationToken);
    }

    public Task<OperationList?> ListOperationsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        var request = CreateRequest(HttpMethod.Get, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/operations");
        return SendForJsonAsync<OperationList>(request, nameof(ListOperationsAsync), cancellationToken);
    }

    public Task<SubscriptionOperation?> GetOperationAsync(string subscriptionId, string operationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        var request = CreateRequest(HttpMethod.Get, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/operations/{Uri.EscapeDataString(operationId)}");
        return SendForJsonAsync<SubscriptionOperation>(request, nameof(GetOperationAsync), cancellationToken);
    }

    public Task PatchOperationAsync(string subscriptionId, string operationId, PatchOperationRequest patchRequest, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(patchRequest);
        var request = CreateRequest(HttpMethod.Patch, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/operations/{Uri.EscapeDataString(operationId)}");
        request.Content = JsonContent.Create(patchRequest, options: JsonOptions);
        return SendAsync(request, nameof(PatchOperationAsync), cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var uri = $"{baseUrl}/saas/{relativePath}?api-version={Uri.EscapeDataString(_options.ApiVersion)}";
        // The token-free emulator identifies the publisher from this query parameter when no
        // bearer token is present. Real production calls carry a bearer token instead.
        if (!string.IsNullOrEmpty(_options.PublisherId))
        {
            uri += $"&publisherId={Uri.EscapeDataString(_options.PublisherId)}";
        }
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("x-ms-requestid", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("x-ms-correlationid", Guid.NewGuid().ToString());
        return request;
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<T?> SendForJsonAsync<T>(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using (request)
        {
            await AuthorizeAsync(request, cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new FulfillmentApiException(response.StatusCode, operation);
            }

            if (response.Content.Headers.ContentLength is 0)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
    }

    private async Task SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using (request)
        {
            await AuthorizeAsync(request, cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new FulfillmentApiException(response.StatusCode, operation);
            }
        }
    }
}
