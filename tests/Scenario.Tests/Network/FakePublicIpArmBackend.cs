using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Stands in for ARM behind the public IP lifecycle, answering the way the committed live
/// recording (<c>SessionRecords/PublicIpScenarioTests</c>) shows the network RP does: create returns
/// 201 + Azure-AsyncOperation, and the stored resource echoes back what the create actually PUT;
/// delete returns 202 + Azure-AsyncOperation + Location, and the Location result is an empty 204.
/// </summary>
internal sealed class FakePublicIpArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private JsonElement _created;

    public FakePublicIpArmBackend(string subscriptionId, string resourceGroupName)
    {
        _subscriptionId = subscriptionId;
        _resourceGroupName = resourceGroupName;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var name = path.TrimEnd('/').Split('/')[^1];

        if (path.Contains("/operations/pip-", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new { status = "Succeeded" }, HttpStatusCode.OK));
        }

        if (path.Contains("/operationResults/pip-delete", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }

        if (!path.Contains("/publicIPAddresses", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
        }

        if (request.Method == HttpMethod.Put)
        {
            var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            var created = Json(Store(name, body, "Updating"), HttpStatusCode.Created);
            created.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/pip-create"));
            _created = Store(name, body, "Succeeded");
            return Task.FromResult(created);
        }

        if (request.Method == HttpMethod.Delete)
        {
            var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/pip-delete"));
            accepted.Headers.TryAddWithoutValidation("Location", OperationUri("operationResults/pip-delete"));
            accepted.Headers.TryAddWithoutValidation("Retry-After", "1");
            return Task.FromResult(accepted);
        }

        // The collection URI ends in the resource type rather than a resource name.
        return Task.FromResult(name.Equals("publicIPAddresses", StringComparison.OrdinalIgnoreCase)
            ? Json(new { value = new[] { _created } }, HttpStatusCode.OK)
            : Json(_created, HttpStatusCode.OK));
    }

    /// <summary>Echoes the PUT body back as a resource in the given provisioning state, the way ARM does.</summary>
    private JsonElement Store(string name, string requestBody, string provisioningState)
    {
        using var document = JsonDocument.Parse(requestBody);
        var resource = new Dictionary<string, object>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            resource[property.Name] = property.Value.Clone();
        }

        var properties = new Dictionary<string, object>();
        foreach (var property in document.RootElement.GetProperty("properties").EnumerateObject())
        {
            properties[property.Name] = property.Value.Clone();
        }
        properties["provisioningState"] = provisioningState;
        properties["ipAddress"] = "20.0.0.1";

        resource["properties"] = properties;
        resource["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{name}";
        resource["name"] = name;
        resource["type"] = "Microsoft.Network/publicIPAddresses";

        return JsonSerializer.SerializeToElement(resource);
    }

    private string OperationUri(string operation) =>
        $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/locations/westus2/{operation}?api-version=2025-07-01";

    // Retry-After 1 keeps recording against this fake quick; playback neutralizes it to zero.
    private static HttpResponseMessage Json(object body, HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "1");
        return response;
    }
}
