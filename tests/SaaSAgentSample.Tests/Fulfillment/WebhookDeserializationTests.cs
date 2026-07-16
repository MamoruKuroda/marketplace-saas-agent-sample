using System.Text.Json;
using SaaSAgentSample.Fulfillment.Models;

namespace SaaSAgentSample.Tests.FulfillmentTests;

public class WebhookDeserializationTests
{
    [Fact]
    public void Deserializes_permissively_ignoring_unknown_fields()
    {
        const string json = """
        {
          "id": "notif-1",
          "action": "Unsubscribe",
          "subscriptionId": "sub-guid",
          "planId": "silver",
          "quantity": 10,
          "timeStamp": "2023-02-10T08:49:01.8613208Z",
          "status": "Succeeded",
          "operationRequestSource": "Azure",
          "purchaseToken": "should-be-ignored",
          "someFutureField": { "nested": true },
          "subscription": {
            "id": "sub-guid",
            "saasSubscriptionStatus": "Unsubscribed",
            "anUnknownFutureProp": 123
          }
        }
        """;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var notification = JsonSerializer.Deserialize<WebhookNotification>(json, options);

        Assert.NotNull(notification);
        Assert.Equal("Unsubscribe", notification!.Action);
        Assert.Equal("sub-guid", notification.SubscriptionId);
        Assert.Equal(10, notification.Quantity);
        Assert.Equal("Unsubscribed", notification.Subscription!.SaasSubscriptionStatus);
    }
}
