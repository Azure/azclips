// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Scenario.Tests;

/// <summary>
/// In-memory Microsoft.ManagedIdentity RP: PUT stores the identity, PATCH merges tags and
/// properties into it, GET returns it (404 when absent), DELETE removes it, and list pages
/// through a nextLink so pagination is exercised.
/// </summary>
internal sealed class FakeIdentityArmBackend : HttpMessageHandler
{
    private const string ProviderSegment = "/providers/Microsoft.ManagedIdentity/userAssignedIdentities";

    private readonly string _subscriptionId;
    private readonly Dictionary<string, JsonObject> _identities = new(StringComparer.OrdinalIgnoreCase);

    public FakeIdentityArmBackend(string subscriptionId)
    {
        _subscriptionId = subscriptionId;
    }

    public List<(HttpMethod Method, string PathAndQuery)> Requests { get; } = [];

    public JsonObject? LastPutBody { get; private set; }

    public JsonObject? LastPatchBody { get; private set; }

    public bool Contains(string name) => _identities.ContainsKey(name);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath.TrimEnd('/');
        Requests.Add((request.Method, uri.PathAndQuery));

        if (path.EndsWith(ProviderSegment, StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
        {
            return List(uri);
        }

        if (!path.Contains(ProviderSegment + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Fake identity ARM got an unexpected request: {request.Method} {uri}");
        }

        var segments = path.Trim('/').Split('/');
        var resourceGroup = segments[3];
        var name = segments[^1];

        if (request.Method == HttpMethod.Put)
        {
            var body = await ReadBody(request, cancellationToken);
            LastPutBody = body.DeepClone().AsObject();
            var identity = Identity(resourceGroup, name, body);
            _identities[name] = identity;
            return Json(identity, HttpStatusCode.Created);
        }

        if (!_identities.TryGetValue(name, out var existing))
        {
            return Json(new JsonObject
            {
                ["error"] = new JsonObject
                {
                    ["code"] = "ResourceNotFound",
                    ["message"] = $"The Resource 'Microsoft.ManagedIdentity/userAssignedIdentities/{name}' was not found.",
                },
            }, HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Get)
        {
            return Json(existing);
        }

        if (request.Method == HttpMethod.Patch)
        {
            var patch = await ReadBody(request, cancellationToken);
            LastPatchBody = patch.DeepClone().AsObject();
            if (patch["tags"] is JsonObject tags)
            {
                existing["tags"] = tags.DeepClone();
            }
            if (patch["properties"] is JsonObject properties)
            {
                var target = existing["properties"]!.AsObject();
                foreach (var (key, value) in properties)
                {
                    target[key] = value?.DeepClone();
                }
            }
            return Json(existing);
        }

        if (request.Method == HttpMethod.Delete)
        {
            _identities.Remove(name);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
        }

        throw new InvalidOperationException($"Fake identity ARM got an unexpected request: {request.Method} {uri}");
    }

    private HttpResponseMessage List(Uri uri)
    {
        var all = _identities.Values.OrderBy(identity => identity["name"]!.GetValue<string>()).ToList();
        var secondPage = uri.Query.Contains("$skiptoken=page2", StringComparison.OrdinalIgnoreCase);
        var page = new JsonArray(all.Skip(secondPage ? 1 : 0).Take(secondPage ? int.MaxValue : 1)
            .Select(identity => (JsonNode)identity.DeepClone()).ToArray());

        var result = new JsonObject { ["value"] = page };
        if (!secondPage && all.Count > 1)
        {
            result["nextLink"] = $"https://management.azure.com{uri.AbsolutePath}?api-version=2025-05-31-preview&$skiptoken=page2";
        }
        return Json(result);
    }

    private JsonObject Identity(string resourceGroup, string name, JsonObject body)
    {
        var properties = body["properties"] as JsonObject ?? new JsonObject();
        properties["tenantId"] = "11111111-1111-1111-1111-111111111111";
        properties["principalId"] = Guid.NewGuid().ToString();
        properties["clientId"] = Guid.NewGuid().ToString();
        properties["isolationScope"] ??= "None";

        return new JsonObject
        {
            ["id"] = $"/subscriptions/{_subscriptionId}/resourceGroups/{resourceGroup}{ProviderSegment}/{name}",
            ["name"] = name,
            ["type"] = "Microsoft.ManagedIdentity/userAssignedIdentities",
            ["location"] = body["location"]?.DeepClone(),
            ["tags"] = body["tags"]?.DeepClone() ?? new JsonObject(),
            ["properties"] = properties.DeepClone(),
        };
    }

    private static async Task<JsonObject> ReadBody(HttpRequestMessage request, CancellationToken cancellationToken) =>
        JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();

    private static HttpResponseMessage Json(JsonNode body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return response;
    }
}
