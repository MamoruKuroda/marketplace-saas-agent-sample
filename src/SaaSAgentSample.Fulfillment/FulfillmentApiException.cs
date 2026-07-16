using System.Net;

namespace SaaSAgentSample.Fulfillment;

/// <summary>Thrown when the Fulfillment API returns a non-success status code.</summary>
public sealed class FulfillmentApiException : Exception
{
    public FulfillmentApiException(HttpStatusCode statusCode, string operation)
        : base($"Fulfillment API call '{operation}' failed with status {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        Operation = operation;
    }

    public HttpStatusCode StatusCode { get; }

    public string Operation { get; }
}
