// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

internal sealed class FakeSubnetArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly string _virtualNetworkName;
    private JsonElement _created;
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
            _created = Store(name, request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("subnet-create"));
            return Task.FromResult(accepted);
        }
        if (request.Method == HttpMethod.Delete)
        {
            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Location", OperationUri("subnet-delete"));
            return Task.FromResult(accepted);
        }

        if (name.Equals("subnets", StringComparison.OrdinalIgnoreCase))
        {
            if (request.RequestUri.Query.Contains("skipToken=second", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(new { value = new[] { CopyCreated("scenario-subnet-page-2") } }, HttpStatusCode.OK));
            }

            return Task.FromResult(Json(new
            {
                value = new[] { _created },
                nextLink = $"{request.RequestUri.GetLeftPart(UriPartial.Path)}?api-version=2025-07-01&skipToken=second"
            }, HttpStatusCode.OK));
        }

        return Task.FromResult(Json(_created, HttpStatusCode.OK));
    }

    private JsonElement Store(string name, string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        var resource = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        var properties = document.RootElement.GetProperty("properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());

        properties["provisioningState"] = "Succeeded";
        resource["properties"] = properties;
        resource["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{_virtualNetworkName}/subnets/{name}";
        resource["name"] = name;
        resource["type"] = "Microsoft.Network/virtualNetworks/subnets";
        return JsonSerializer.SerializeToElement(resource);
    }

    private string OperationUri(string operation) =>
        $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/locations/westus2/operations/{operation}?api-version=2025-07-01";

    private JsonElement CopyCreated(string name)
    {
        var resource = _created.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        resource["name"] = name;
        resource["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{_virtualNetworkName}/subnets/{name}";
        return JsonSerializer.SerializeToElement(resource);
    }

    private JsonElement StoreVirtualNetwork(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        var resource = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        var properties = document.RootElement.GetProperty("properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => (object)property.Value.Clone());

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
