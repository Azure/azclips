using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Stands in for ARM behind the network interface lifecycle so the scenario can be recorded
/// without a live subscription, exactly like <see cref="FakePublicIpArmBackend"/>: create returns
/// 202 + Azure-AsyncOperation, and the stored resource echoes back what the create actually PUT,
/// so an assertion on the command output is an assertion on the request body the surface produced.
/// Delete answers the way the network RP does (see azure-cli's <c>test_network_nic.yaml</c>):
/// 202 + Azure-AsyncOperation + Location, and the Location result is an empty 204.
/// </summary>
internal sealed class FakeNetworkInterfaceArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private JsonElement _created;

    public FakeNetworkInterfaceArmBackend(string subscriptionId, string resourceGroupName)
    {
        _subscriptionId = subscriptionId;
        _resourceGroupName = resourceGroupName;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var name = path.TrimEnd('/').Split('/')[^1];

        if (path.Contains("/operations/nic-delete", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/operations/nic-create", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new { status = "Succeeded" }, HttpStatusCode.OK));
        }

        if (path.Contains("/operationResults/nic-delete", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }

        if (!path.Contains("/networkInterfaces", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
        }

        if (request.Method == HttpMethod.Put)
        {
            var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            _created = Store(name, body);

            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/nic-create"));
            return Task.FromResult(accepted);
        }

        if (request.Method == HttpMethod.Delete)
        {
            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/nic-delete"));
            accepted.Headers.TryAddWithoutValidation("Location", OperationUri("operationResults/nic-delete"));
            return Task.FromResult(accepted);
        }

        // The collection URI ends in the resource type rather than a resource name.
        return Task.FromResult(name.Equals("networkInterfaces", StringComparison.OrdinalIgnoreCase)
            ? Json(new { value = new[] { _created } }, HttpStatusCode.OK)
            : Json(_created, HttpStatusCode.OK));
    }

    /// <summary>Echoes the PUT body back as a provisioned resource, the way ARM does.</summary>
    private JsonElement Store(string name, string requestBody)
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
        properties["provisioningState"] = "Succeeded";
        properties["macAddress"] = "00-0D-3A-00-00-01";

        resource["properties"] = properties;
        resource["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/networkInterfaces/{name}";
        resource["name"] = name;
        resource["type"] = "Microsoft.Network/networkInterfaces";

        return JsonSerializer.SerializeToElement(resource);
    }

    private string OperationUri(string operation) =>
        $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/locations/westus2/{operation}?api-version=2025-07-01";

    // These request-shape scenarios verify polling, not elapsed retry pacing.
    private static HttpResponseMessage Json(object body, HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return response;
    }
}
