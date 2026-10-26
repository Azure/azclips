// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Answers subnet requests the way the network RP does: create returns 201 + Azure-AsyncOperation,
/// delete returns 202 + Azure-AsyncOperation + Location whose operation result is an empty 204,
/// and a deleted subnet then reads as 404. Subnets declared in a virtual network PUT become
/// visible to the subnet GET/LIST routes, like child resources in ARM.
/// </summary>
internal sealed class FakeSubnetArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly string _virtualNetworkName;
    private readonly Dictionary<string, JsonElement> _subnets = new(StringComparer.OrdinalIgnoreCase);
    private JsonElement _virtualNetwork;

    public FakeSubnetArmBackend(string subscriptionId, string resourceGroupName, string virtualNetworkName)
    {
        _subscriptionId = subscriptionId;
        _resourceGroupName = resourceGroupName;
        _virtualNetworkName = virtualNetworkName;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var name = path.TrimEnd('/').Split('/')[^1];

        if (path.Contains("/operationResults/subnet-", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
        if (path.Contains("/operations/subnet-", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new { status = "Succeeded" }, HttpStatusCode.OK));
        }
        if (request.Method == HttpMethod.Put
            && path.EndsWith($"/virtualNetworks/{_virtualNetworkName}", StringComparison.OrdinalIgnoreCase))
        {
            _virtualNetwork = StoreVirtualNetwork(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            return Task.FromResult(Json(_virtualNetwork, HttpStatusCode.OK));
        }
        if (request.Method == HttpMethod.Get
            && path.EndsWith($"/virtualNetworks/{_virtualNetworkName}", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(_virtualNetwork, HttpStatusCode.OK));
        }
        if (!path.Contains("/subnets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
        }
        if (request.Method == HttpMethod.Put)
        {
            var created = Store(name, request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            var response = Json(created, HttpStatusCode.Created);
            response.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/subnet-create"));
            return Task.FromResult(response);
        }
        if (request.Method == HttpMethod.Delete)
        {
            _subnets.Remove(name);
            var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/subnet-delete"));
            accepted.Headers.TryAddWithoutValidation("Location", OperationUri("operationResults/subnet-delete"));
            accepted.Headers.TryAddWithoutValidation("Retry-After", "1");
            return Task.FromResult(accepted);
        }

        if (name.Equals("subnets", StringComparison.OrdinalIgnoreCase))
        {
            if (request.RequestUri.Query.Contains("skipToken=second", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(new { value = _subnets.Values.Take(1).Select(s => Copy(s, $"{s.GetProperty("name").GetString()}-page-2")).ToArray() }, HttpStatusCode.OK));
            }

            return Task.FromResult(Json(new
            {
                value = _subnets.Values.ToArray(),
                nextLink = $"{request.RequestUri.GetLeftPart(UriPartial.Path)}?api-version=2025-07-01&skipToken=second"
            }, HttpStatusCode.OK));
        }

        return Task.FromResult(_subnets.TryGetValue(name, out var subnet)
            ? Json(subnet, HttpStatusCode.OK)
            : Json(new { error = new { code = "NotFound", message = $"Resource {path} not found." } }, HttpStatusCode.NotFound));
    }

    private JsonElement Store(string name, string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        return _subnets[name] = Normalize(name, document.RootElement);
    }

    private JsonElement Normalize(string name, JsonElement subnet)
    {
        var resource = subnet.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        var properties = subnet.GetProperty("properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());

        properties["provisioningState"] = "Succeeded";
        resource["properties"] = properties;
        resource["id"] = SubnetId(name);
        resource["name"] = name;
        resource["type"] = "Microsoft.Network/virtualNetworks/subnets";
        return JsonSerializer.SerializeToElement(resource);
    }

    private string SubnetId(string name) =>
        $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{_virtualNetworkName}/subnets/{name}";

    private string OperationUri(string operation) =>
        $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/locations/westus2/{operation}?api-version=2025-07-01";

    private JsonElement Copy(JsonElement subnet, string name)
    {
        var resource = subnet.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        resource["name"] = name;
        resource["id"] = SubnetId(name);
        return JsonSerializer.SerializeToElement(resource);
    }

    private JsonElement StoreVirtualNetwork(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        var resource = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        var properties = document.RootElement.GetProperty("properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());

        if (document.RootElement.GetProperty("properties").TryGetProperty("subnets", out var subnets))
        {
            foreach (var subnet in subnets.EnumerateArray())
            {
                var subnetName = subnet.GetProperty("name").GetString()!;
                _subnets[subnetName] = Normalize(subnetName, subnet);
            }
            properties["subnets"] = _subnets.Values.ToArray();
        }

        properties["provisioningState"] = "Succeeded";
        resource["properties"] = properties;
        resource["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{_virtualNetworkName}";
        resource["name"] = _virtualNetworkName;
        return JsonSerializer.SerializeToElement(resource);
    }

    private static HttpResponseMessage Json(object body, HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "1");
        return response;
    }
}