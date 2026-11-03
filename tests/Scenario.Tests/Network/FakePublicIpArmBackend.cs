using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Stands in for ARM behind the public IP lifecycle so the scenario can be recorded without a live
/// subscription. The create and delete are real LROs: create returns 202 + Azure-AsyncOperation and
/// the runtime re-reads the resource URI afterwards, delete returns 202 + Location and the runtime
/// polls that same URI. The stored resource echoes back what the create actually PUT, so an
/// assertion on the command output is an assertion on the request body the surface produced.
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

        if (path.Contains("/operations/pip-delete", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new { status = "Succeeded" }, HttpStatusCode.OK));
        }

        if (path.Contains("/operations/pip-create", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new { status = "Succeeded" }, HttpStatusCode.OK));
        }

        if (!path.Contains("/publicIPAddresses", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
        }

        if (request.Method == HttpMethod.Put)
        {
            var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            _created = Store(name, body);

            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("pip-create"));
            return Task.FromResult(accepted);
        }

        if (request.Method == HttpMethod.Delete)
        {
            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Location", OperationUri("pip-delete"));
            return Task.FromResult(accepted);
        }

        // The collection URI ends in the resource type rather than a resource name.
        return Task.FromResult(name.Equals("publicIPAddresses", StringComparison.OrdinalIgnoreCase)
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
        properties["ipAddress"] = "20.0.0.1";

        resource["properties"] = properties;
        resource["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{name}";
        resource["name"] = name;
        resource["type"] = "Microsoft.Network/publicIPAddresses";

        return JsonSerializer.SerializeToElement(resource);
    }

    private string OperationUri(string operation) =>
        $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/locations/westus2/operations/{operation}?api-version=2025-07-01";

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
