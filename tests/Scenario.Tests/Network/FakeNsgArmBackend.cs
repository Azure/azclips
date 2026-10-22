// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

internal sealed class FakeNsgArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;

    public FakeNsgArmBackend(string subscriptionId)
    {
        _subscriptionId = subscriptionId;
    }

    public Dictionary<string, string>? LastCreateTags { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath;
        var name = path.TrimEnd('/').Split('/')[^1];

        if (path.Contains("/operations/nsg-delete", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { status = "Succeeded" });
        }

        if (path.Contains("/operationResults/nsg-delete", StringComparison.OrdinalIgnoreCase))
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Get &&
            path.EndsWith($"/resourceGroups/{name}", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { name, location = "eastus" });
        }

        if (request.Method == HttpMethod.Put && path.Contains("/networkSecurityGroups/", StringComparison.OrdinalIgnoreCase))
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var tags = body.RootElement.TryGetProperty("tags", out var tagElement)
                ? tagElement.EnumerateObject().ToDictionary(tag => tag.Name, tag => tag.Value.GetString()!)
                : new Dictionary<string, string>();
            LastCreateTags = tags;
            return Json(Nsg(name, body.RootElement.GetProperty("location").GetString()!, tags));
        }

        if (request.Method == HttpMethod.Get &&
            path.EndsWith("/networkSecurityGroups", StringComparison.OrdinalIgnoreCase))
        {
            if (uri.Query.Contains("$skiptoken=page2", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { value = new[] { Nsg("second-nsg", "eastus", new Dictionary<string, string> { ["owner"] = "second" }) } });
            }

            return Json(new
            {
                value = new[] { Nsg("scenario-nsg", "eastus", new Dictionary<string, string> { ["owner"] = "scenario" }) },
                nextLink = $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/networkSecurityGroups?api-version=2025-07-01&$skiptoken=page2",
            });
        }

        if (request.Method == HttpMethod.Get && path.Contains("/networkSecurityGroups/", StringComparison.OrdinalIgnoreCase))
        {
            return Json(Nsg(name, "eastus", new Dictionary<string, string> { ["owner"] = "scenario" }));
        }

        // Delete answers the way the network RP does: 202 + Azure-AsyncOperation + Location, and the
        // Location result is an empty 204.
        if (request.Method == HttpMethod.Delete && path.Contains("/networkSecurityGroups/", StringComparison.OrdinalIgnoreCase))
        {
            var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", OperationUri("operations/nsg-delete"));
            accepted.Headers.TryAddWithoutValidation("Location", OperationUri("operationResults/nsg-delete"));
            return accepted;
        }

        throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {uri}");
    }

    private object Nsg(string name, string location, Dictionary<string, string> tags) => new
    {
        id = $"/subscriptions/{_subscriptionId}/resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups/{name}",
        name,
        type = "Microsoft.Network/networkSecurityGroups",
        location,
        tags,
        properties = new
        {
            provisioningState = "Succeeded",
            defaultSecurityRules = new[] { new { name = "AllowVnetInBound" } },
        },
    };

    private string OperationUri(string relative) =>
        $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Network/locations/eastus/{relative}?api-version=2025-07-01";

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return response;
    }
}
