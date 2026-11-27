using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cli.Tests.Fixtures;

namespace Scenario.Tests;

internal sealed class FakePublicIpDeploymentBackend(string scenario = "Default", string? fault = null, Action? onPoll = null) : HttpMessageHandler
{
    private readonly JsonElement recorded = ReadScenario(scenario);
    private bool submitted;
    internal int SubmissionCount { get; private set; }
    internal int ResourceGetCount { get; private set; }
    internal List<string> Requests { get; } = [];

    internal static JsonElement ReadScenario(string scenario)
    {
        using Stream stream = PublicIpBicepAssets.OpenResource("PublicIpBicepSnapshots.json")
            ?? throw new InvalidOperationException("Independent Public IP snapshots are missing.");
        using var evidence = JsonDocument.Parse(stream);
        return evidence.RootElement.GetProperty("cases").GetProperty(scenario).Clone();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        Requests.Add($"{request.Method} {request.RequestUri}");
        if (path.Contains("/publicIPAddresses/", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("?api-version=2026-03-01", request.RequestUri.Query);
            ResourceGetCount++;
            if (!submitted)
                return Json(fault == "Existing" ? """{"name":"my-pip"}""" : """{"error":{"code":"ResourceNotFound"}}""",
                    fault == "Existing" ? HttpStatusCode.OK : fault == "PreflightForbidden" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound);
            if (fault == "FinalGetFailed") return Json("""{"error":{"code":"AuthorizationFailed"}}""", HttpStatusCode.Forbidden);
            if (fault == "FinalGetEmpty") return Json("{}");
            var resource = JsonNode.Parse(recorded.GetProperty("snapshot").GetProperty("predictedResources")[0].GetRawText())!.AsObject();
            resource.Remove("apiVersion");
            resource["id"] = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
            resource["properties"]!["provisioningState"] = "Succeeded";
            resource["properties"]!["ipAddress"] = "20.0.0.1";
            return Json(resource.ToJsonString());
        }
        Assert.Equal("?api-version=2025-04-01", request.RequestUri.Query);
        if (path.EndsWith("/operations/public-ip-offline-proof", StringComparison.Ordinal))
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            onPoll?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            string state = fault == "DeploymentFailed" ? "Failed" : fault == "DeploymentCanceled" ? "Canceled" : "Succeeded";
            return Json($$"""{"status":"{{state}}"}""");
        }
        Assert.Contains("/providers/Microsoft.Resources/deployments/azclips-public-ip-offline-proof", path, StringComparison.OrdinalIgnoreCase);
        if (request.Method == HttpMethod.Put)
        {
            SubmissionCount++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            JsonElement properties = body.RootElement.GetProperty("properties");
            Assert.Equal(new[] { "mode", "parameters", "template" },
                properties.EnumerateObject().Select(property => property.Name).Order());
            Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
            string variant = recorded.GetProperty("variant").GetString()!;
            using Stream asset = PublicIpBicepAssets.OpenResource($"PublicIpBicepCreate.{variant}.json")!;
            using var expected = JsonDocument.Parse(asset);
            Assert.True(JsonElement.DeepEquals(expected.RootElement, properties.GetProperty("template")));
            JsonElement expectedParameters = recorded.GetProperty("parameters");
            JsonElement parameters = properties.GetProperty("parameters");
            Assert.Equal(expectedParameters.EnumerateObject().Select(parameter => parameter.Name).Order(),
                parameters.EnumerateObject().Select(parameter => parameter.Name).Order());
            foreach (JsonProperty parameter in parameters.EnumerateObject())
            {
                Assert.Equal(new[] { "value" }, parameter.Value.EnumerateObject().Select(property => property.Name));
                Assert.True(JsonElement.DeepEquals(expectedParameters.GetProperty(parameter.Name).GetProperty("value"),
                    parameter.Value.GetProperty("value")));
            }
            if (fault == "SubmissionForbidden") return Json("""{"error":{"code":"AuthorizationFailed"}}""", HttpStatusCode.Forbidden);
            submitted = true;
            var accepted = Json("""{"status":"Running"}""", HttpStatusCode.Accepted);
            accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation",
                "https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Resources/locations/eastus/operations/public-ip-offline-proof?api-version=2025-04-01");
            return accepted;
        }
        Assert.Equal(HttpMethod.Get, request.Method);
        string finalState = fault == "DeploymentCanceled" ? "Canceled" : fault == "FinalDeploymentFailed" ? "Failed" : "Succeeded";
        return Json(fault == "DeploymentMissingState" ? """{"name":"azclips-public-ip-offline-proof","properties":{}}""" :
            JsonSerializer.Serialize(new { name = "azclips-public-ip-offline-proof", properties = new { provisioningState = finalState } }));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return response;
    }
}
