using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class TenantDeploymentScenarioTests
{
    private const string DeploymentName = "scenario-tenant-deployment";
    private const string CancelDeploymentName = "scenario-tenant-deployment-cancel";
    private const string ResourceGroupName = "scenario-tenant-deployment-rg";
    private const string WhatIfDeploymentName = "scenario-tenant-what-if";
    private const string WhatIfResourceGroupName = "scenario-tenant-what-if-rg";
    private const string SubscriptionId = RecordingSanitizers.PlaceholderGuid;
    private const string Location = "WestUS";
    private const string DeploymentsPath = "/providers/Microsoft.Resources/deployments/";
    private const string SucceededFilter = "provisioningState eq 'Succeeded'";
    private const string TenantTemplate = """
        {
          "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "parameters": {
            "nestedSubId": { "type": "string" },
            "nestedRG": { "type": "string" }
          },
          "resources": [
            {
              "type": "Microsoft.Resources/deployments",
              "apiVersion": "2021-04-01",
              "name": "sub-nested",
              "subscriptionId": "[parameters('nestedSubId')]",
              "location": "East US 2",
              "properties": {
                "mode": "Incremental",
                "template": {
                  "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
                  "contentVersion": "1.0.0.0",
                  "resources": [
                    {
                      "type": "Microsoft.Resources/resourceGroups",
                      "apiVersion": "2021-04-01",
                      "name": "[parameters('nestedRG')]",
                      "location": "East US 2"
                    }
                  ]
                }
              }
            }
          ]
        }
        """;

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.ResourcesDeployments, ExtensionUnderTest.Rest];

    [Fact]
    public void TenantDeploymentScenarios()
    {
        // Keep lifecycle, cancellation, and what-if in the existing shared recording.
        var recording = SessionRecordFiles.OpenSession(
            typeof(TenantDeploymentScenarioTests), nameof(TenantDeploymentScenarioTests),
            new DefaultRequestMatcher(matchBody: true));

        if (recording.Mode == RecorderMode.Record)
        {
            string deployment = TestRun.ScopedName(DeploymentName, recording.Mode);
            string cancelDeployment = TestRun.ScopedName(CancelDeploymentName, recording.Mode);
            string resourceGroup = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            RecordPlaybackInterceptor requests = recording.CreateInterceptor();
            using var host = CreateRecordHost(requests, deployment);
            recording.Record.Metadata["backend"] = "fake";
            bool succeeded = false;
            try
            {
                LifecycleCli(host, requests, deployment, resourceGroup);
                RunCancellation(host, requests, ClientType.CLI, cancelDeployment, resourceGroup);
                RunCliWhatIf(host, requests,
                    TestRun.ScopedName(WhatIfDeploymentName, recording.Mode),
                    TestRun.ScopedName(WhatIfResourceGroupName, recording.Mode));
                recording.Save();
                succeeded = true;
            }
            finally
            {
                // Save first so fallback cleanup is not part of the shared workflow recording.
                foreach (string name in new[] { deployment, cancelDeployment })
                {
                    try
                    {
                        host.RunCli("deployment", "tenant", "delete", "--name", name);
                    }
                    catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound)
                    {
                    }
                    catch (Exception error) when (!succeeded)
                    {
                        Console.Error.WriteLine($"Cleanup of tenant deployment '{name}' failed: {error}");
                    }
                }
            }
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliRequests, extensions: s_extensions))
        {
            LifecycleCli(cli, cliRequests, DeploymentName, ResourceGroupName);
            RunCancellation(cli, cliRequests, ClientType.CLI, CancelDeploymentName, ResourceGroupName);
            RunCliWhatIf(cli, cliRequests, WhatIfDeploymentName, WhatIfResourceGroupName);
            Assert.Equal(recording.Record.Entries.Count, cliRequests.Sent.Count);
        }

        RecordPlaybackInterceptor psRequests = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(ClientType.PowerShell, psRequests, extensions: s_extensions))
        {
            LifecyclePowerShell(ps, psRequests);
            RunCancellation(ps, psRequests, ClientType.PowerShell, CancelDeploymentName, ResourceGroupName);
            RunPowerShellWhatIf(ps, psRequests);
            Assert.Equal(recording.Record.Entries.Count, psRequests.Sent.Count);
        }
    }

    private static void RunCliWhatIf(ScenarioHost host, RecordPlaybackInterceptor requests, string deployment, string resourceGroup) =>
        WithTemplateFile(template =>
        {
            AssertWhatIf(host.RunCli(DeploymentArguments("what-if", deployment, resourceGroup, template)), resourceGroup);
            AssertWhatIfRequest(requests, deployment, resourceGroup);
        });

    private static void RunPowerShellWhatIf(ScenarioHost host, RecordPlaybackInterceptor requests) =>
        WithTemplateFile(template =>
        {
            AssertWhatIf(host.RunPowerShell("Get-AzTenantDeploymentWhatIfResult",
                DeploymentParameters(WhatIfDeploymentName, WhatIfResourceGroupName, template)), WhatIfResourceGroupName);
            AssertWhatIfRequest(requests, WhatIfDeploymentName, WhatIfResourceGroupName);
        });

    // This is the fake-only recording seam. Live recording replaces the backend with useLiveAuth: true.
    private static ScenarioHost CreateRecordHost(RecordPlaybackInterceptor requests, string deployment) =>
        new(ClientType.CLI, requests, new TenantDeploymentBackend(deploymentName: deployment),
            extensions: s_extensions);

    private static void RunCancellation(
        ScenarioHost host, RecordPlaybackInterceptor requests, ClientType surface, string deployment, string resourceGroup)
    {
        JsonObject template = JsonNode.Parse(TenantTemplate)!.AsObject();
        template["resources"]!.AsArray().Add(JsonNode.Parse("""
            {
              "type": "Microsoft.Resources/deployments",
              "apiVersion": "2021-04-01",
              "name": "[format('slow-{0}', copyIndex())]",
              "subscriptionId": "[parameters('nestedSubId')]",
              "resourceGroup": "[parameters('nestedRG')]",
              "dependsOn": ["sub-nested"],
              "copy": { "name": "slow", "count": 10, "mode": "serial", "batchSize": 1 },
              "properties": {
                "mode": "Incremental",
                "template": {
                  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                  "contentVersion": "1.0.0.0",
                  "resources": []
                }
              }
            }
            """));
        string body = new JsonObject
        {
            ["location"] = Location,
            ["properties"] = new JsonObject
            {
                ["mode"] = "Incremental",
                ["template"] = template,
                ["parameters"] = new JsonObject
                {
                    ["nestedSubId"] = new JsonObject { ["value"] = SubscriptionId },
                    ["nestedRG"] = new JsonObject { ["value"] = resourceGroup },
                },
            },
        }.ToJsonString();
        string uri = $"https://management.azure.com{DeploymentsPath}{deployment}?api-version=2025-04-01";
        var named = new Dictionary<string, object> { ["Name"] = deployment };

        // Normal create waits for completion; a raw PUT leaves this deployment cancellable.
        var submitted = Assert.IsType<RestResponse>(Assert.Single(surface == ClientType.CLI
            ? host.RunCli("rest", "--method", "PUT", "--uri", uri, "--body", body)
            : host.RunPowerShell("Invoke-AzRestMethod", new Dictionary<string, object>
            {
                ["Method"] = "PUT",
                ["Uri"] = uri,
                ["Payload"] = body,
            })));
        Assert.Equal(201, submitted.StatusCode);
        using var response = JsonDocument.Parse(submitted.Content);
        Assert.Equal("Accepted", response.RootElement.GetProperty("properties").GetProperty("provisioningState").GetString());
        CapturedHttpRequest put = LastSent(requests, "PUT", DeploymentsPath + deployment + "?");
        AssertRequest(put, "PUT", DeploymentsPath + deployment);
        AssertDeploymentBody(put, resourceGroup, template.ToJsonString());

        IReadOnlyList<object> Show() => surface == ClientType.CLI
            ? host.RunCli("deployment", "tenant", "show", "--name", deployment)
            : host.RunPowerShell("Get-AzTenantDeployment_Get", named);

        AssertDeployment(Show(), deployment, "Running");
        Assert.Empty(surface == ClientType.CLI
            ? host.RunCli("deployment", "tenant", "cancel", "--name", deployment)
            : host.RunPowerShell("Stop-AzTenantDeployment", named));
        CapturedHttpRequest cancel = LastSent(requests, "POST", DeploymentsPath + deployment + "/cancel?");
        AssertRequest(cancel, "POST", DeploymentsPath + deployment + "/cancel");
        Assert.True(string.IsNullOrEmpty(cancel.Body));
        AssertDeployment(Show(), deployment, "Canceled");
        Assert.Empty(surface == ClientType.CLI
            ? host.RunCli("deployment", "tenant", "delete", "--name", deployment)
            : host.RunPowerShell("Remove-AzTenantDeployment", named));
        AssertRequest(LastSent(requests, "DELETE", DeploymentsPath + deployment + "?"), "DELETE", DeploymentsPath + deployment);
    }

    private static void LifecycleCli(ScenarioHost host, RecordPlaybackInterceptor sent, string deployment, string resourceGroup) => WithTemplateFile(template =>
    {
        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(
            host.RunCli(DeploymentArguments("validate", deployment, resourceGroup, template))));
        Assert.Equal("Succeeded", validation.ProvisioningState);
        AssertDeploymentBody(LastSent(sent, "POST", "/validate?"), resourceGroup);

        int beforeCreate = sent.Sent.Count;
        var created = AssertDeployment(
            host.RunCli(DeploymentArguments("create", deployment, resourceGroup, template)), deployment, "Succeeded");
        Assert.Equal("Incremental", created.Mode);
        AssertValidateThenPut(sent, beforeCreate, deployment);

        AssertListed(host.RunCli("deployment", "tenant", "list"), deployment);
        AssertRequest(LastSent(sent, "GET", "/deployments?"), "GET", DeploymentsPath.TrimEnd('/'));
        AssertListed(host.RunCli("deployment", "tenant", "list", "--filter", SucceededFilter), deployment);
        AssertRequest(LastSent(sent, "GET", "$filter="), "GET", DeploymentsPath.TrimEnd('/'),
            "&$filter=" + Uri.EscapeDataString(SucceededFilter));

        AssertDeployment(host.RunCli("deployment", "tenant", "show", "--name", deployment), deployment, "Succeeded");
        AssertExportedTemplate(Assert.IsType<JsonElement>(Assert.Single(
            host.RunCli("deployment", "tenant", "export", "--name", deployment))));
        AssertRequest(LastSent(sent, "POST", "/exportTemplate?"), "POST", DeploymentsPath + deployment + "/exportTemplate");

        Assert.Empty(host.RunCli("deployment", "tenant", "delete", "--name", deployment));
        AssertRequest(LastSent(sent, "DELETE", DeploymentsPath + deployment + "?"), "DELETE", DeploymentsPath + deployment);
    });

    private static void LifecyclePowerShell(ScenarioHost host, RecordPlaybackInterceptor sent)
    {
        WithTemplateFile(template =>
        {
            Dictionary<string, object> body = DeploymentParameters(DeploymentName, ResourceGroupName, template);
            var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunPowerShell("Test-AzTenantDeployment", body)));
            Assert.Equal("Succeeded", validation.ProvisioningState);
            AssertDeploymentBody(LastSent(sent, "POST", "/validate?"), ResourceGroupName);
            int beforeCreate = sent.Sent.Count;
            var created = AssertDeployment(host.RunPowerShell("New-AzTenantDeployment", body), DeploymentName, "Succeeded");
            Assert.Equal("Incremental", created.Mode);
            AssertValidateThenPut(sent, beforeCreate, DeploymentName);
        });

        AssertListed(host.RunPowerShell("Get-AzTenantDeployment_List", new Dictionary<string, object>()), DeploymentName);
        AssertListed(host.RunPowerShell("Get-AzTenantDeployment_List", new Dictionary<string, object> { ["Filter"] = SucceededFilter }), DeploymentName);
        AssertDeployment(host.RunPowerShell("Get-AzTenantDeployment_Get",
            new Dictionary<string, object> { ["Name"] = DeploymentName }), DeploymentName, "Succeeded");

        string saveDirectory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            host.RunPowerShell("Save-AzTenantDeploymentTemplate", new Dictionary<string, object> { ["Name"] = DeploymentName, ["Path"] = saveDirectory });
            string saved = Path.Combine(saveDirectory, DeploymentName + ".json");
            using var document = JsonDocument.Parse(File.ReadAllText(saved));
            AssertExportedTemplate(document.RootElement);
        }
        finally
        {
            Directory.Delete(saveDirectory, recursive: true);
        }

        Assert.Empty(host.RunPowerShell("Remove-AzTenantDeployment", new Dictionary<string, object> { ["Name"] = DeploymentName }));
        AssertRequest(LastSent(sent, "DELETE", DeploymentsPath + DeploymentName + "?"), "DELETE", DeploymentsPath + DeploymentName);
    }

    private static string[] DeploymentArguments(string command, string deployment, string resourceGroup, string template) =>
    [
        "deployment", "tenant", command, "--name", deployment, "--location", Location, "--template-file", template,
        "--parameter", $"nestedSubId={SubscriptionId}", "--parameter", $"nestedRG={resourceGroup}",
    ];

    private static Dictionary<string, object> DeploymentParameters(string deployment, string resourceGroup, string template) => new()
    {
        ["Name"] = deployment,
        ["Location"] = Location,
        ["TemplateFile"] = template,
        ["Parameter"] = new[] { $"nestedSubId={SubscriptionId}", $"nestedRG={resourceGroup}" },
    };

    private static DeploymentExtended AssertDeployment(IReadOnlyList<object> output, string name, string state)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(output));
        Assert.Equal(name, deployment.Name);
        Assert.Equal(state, deployment.ProvisioningState);
        return deployment;
    }

    private static void AssertListed(IReadOnlyList<object> output, string name) =>
        Assert.Contains(Deployments(output), deployment => deployment.Name == name);

    private static void AssertRequest(CapturedHttpRequest request, string method, string path, string query = "")
    {
        Assert.Equal(method, request.Method);
        Assert.Equal(new Uri($"https://management.azure.com{path}?api-version=2025-04-01{query}").AbsoluteUri, request.Uri);
    }

    private static void AssertDeploymentBody(CapturedHttpRequest request, string resourceGroup, string template = TenantTemplate)
    {
        JsonElement body = Body(request);
        Assert.Equal(Location, body.GetProperty("location").GetString());
        JsonElement properties = body.GetProperty("properties");
        Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
        Assert.False(properties.TryGetProperty("templateLink", out _));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(template), JsonNode.Parse(properties.GetProperty("template").GetRawText())));
        JsonElement parameters = properties.GetProperty("parameters");
        Assert.Equal(SubscriptionId, parameters.GetProperty("nestedSubId").GetProperty("value").GetString());
        Assert.Equal(resourceGroup, parameters.GetProperty("nestedRG").GetProperty("value").GetString());
    }

    private static void AssertValidateThenPut(RecordPlaybackInterceptor requests, int beforeCreate, string name)
    {
        CapturedHttpRequest[] sent = requests.Sent.Skip(beforeCreate).ToArray();
        CapturedHttpRequest validate = Assert.Single(sent, request => request.Method == "POST");
        CapturedHttpRequest put = Assert.Single(sent, request => request.Method == "PUT");
        AssertRequest(validate, "POST", DeploymentsPath + name + "/validate");
        AssertRequest(put, "PUT", DeploymentsPath + name);
        Assert.True(Array.IndexOf(sent, validate) < Array.IndexOf(sent, put));
        Assert.Equal(validate.Body, put.Body);
    }

    private static void AssertExportedTemplate(JsonElement template)
    {
        Assert.True(template.TryGetProperty("$schema", out _));
        Assert.False(template.TryGetProperty("template", out _));
        Assert.Equal(1, template.GetProperty("resources").GetArrayLength());
    }

    private static void AssertWhatIf(IReadOnlyList<object> output, string resourceGroup)
    {
        var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output));
        Assert.Equal("Succeeded", result.Status);
        WhatIfChange change = Assert.Single(result.Changes!);
        Assert.Equal("Create", change.ChangeType);
        Assert.Equal($"/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroup}", change.ResourceId);
    }

    private static void AssertWhatIfRequest(RecordPlaybackInterceptor requests, string deployment, string resourceGroup)
    {
        CapturedHttpRequest request = LastSent(requests, "POST", "/whatIf?");
        AssertRequest(request, "POST", DeploymentsPath + deployment + "/whatIf");
        AssertDeploymentBody(request, resourceGroup);
        Assert.Equal("FullResourcePayloads", Body(request).GetProperty("properties").GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
    }

    private static IEnumerable<DeploymentExtended> Deployments(IReadOnlyList<object> output) =>
        output.SelectMany(o => o as IEnumerable<DeploymentExtended> ?? [Assert.IsType<DeploymentExtended>(o)]);

    private static CapturedHttpRequest LastSent(RecordPlaybackInterceptor sent, string method, string uriPart) =>
        Assert.Single(sent.Sent.Where(r => r.Method == method && r.Uri.Contains(uriPart, StringComparison.OrdinalIgnoreCase)).TakeLast(1));

    private static JsonElement Body(CapturedHttpRequest request) => JsonDocument.Parse(request.Body!).RootElement;

    private static void WithTemplateFile(Action<string> run)
    {
        string template = Path.GetTempFileName();
        try
        {
            File.WriteAllText(template, TenantTemplate);
            run(template);
        }
        finally
        {
            File.Delete(template);
        }
    }

    private sealed class TenantDeploymentBackend(string deploymentName) : HttpMessageHandler
    {
        private const string OperationResultsPath = "/providers/Microsoft.Resources/operationResults/";
        private const string OperationId = "00000000-0000-0000-0000-000000000001";
        private const string OperationResultsUri = $"https://management.azure.com{OperationResultsPath}{OperationId}?api-version=2025-04-01";
        private JsonObject? _deploymentBody;
        private JsonObject? _whatIfResult;
        private string _state = "Succeeded";
        private bool _deleted;
        private bool _pollingDeletion;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string name = path.StartsWith(DeploymentsPath, StringComparison.OrdinalIgnoreCase)
                ? path[DeploymentsPath.Length..].Split('/')[0]
                : DeploymentName;

            if (request.Method == HttpMethod.Post && path.EndsWith("/validate", StringComparison.OrdinalIgnoreCase))
            {
                return Json(Deployment(name, await ReadBody(request, cancellationToken), "Succeeded", validated: true).ToJsonString());
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/whatIf", StringComparison.OrdinalIgnoreCase))
            {
                JsonObject body = await ReadBody(request, cancellationToken);
                _pollingDeletion = false;
                string? resourceGroup = body["properties"]?["parameters"]?["nestedRG"]?["value"]?.GetValue<string>();
                _whatIfResult = new JsonObject
                {
                    ["status"] = "Succeeded",
                    ["properties"] = new JsonObject
                    {
                        ["correlationId"] = SubscriptionId,
                        ["changes"] = new JsonArray(),
                    },
                };
                if (resourceGroup is not null)
                {
                    string id = ResourceGroupId(body);
                    _whatIfResult["properties"]!["changes"]!.AsArray().Add(new JsonObject
                    {
                        ["resourceId"] = id,
                        ["changeType"] = "Create",
                        ["after"] = new JsonObject
                        {
                            ["apiVersion"] = "2021-04-01",
                            ["id"] = id,
                            ["location"] = "East US 2",
                            ["name"] = resourceGroup,
                            ["type"] = "Microsoft.Resources/resourceGroups",
                        },
                    });
                }
                return Accepted();
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/exportTemplate", StringComparison.OrdinalIgnoreCase))
            {
                JsonNode template = _deploymentBody?["properties"]?["template"]?.DeepClone()
                    ?? throw new InvalidOperationException("No template was deployed to export.");
                if (template?["parameters"] is JsonObject parameters)
                {
                    foreach ((string _, JsonNode? parameter) in parameters)
                    {
                        if (parameter?["type"]?.GetValue<string>() == "string")
                        {
                            parameter["type"] = "String";
                        }
                    }
                }
                return Json(new JsonObject { ["template"] = template }.ToJsonString());
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (_deleted || _state != "Running")
                {
                    return Json("""{"error":{"code":"DeploymentCannotBeCancelled"}}""", HttpStatusCode.BadRequest);
                }
                _state = "Canceled";
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Put)
            {
                _deploymentBody = await ReadBody(request, cancellationToken);
                _state = "Accepted";
                _deleted = false;
                _pollingDeletion = false;
                HttpResponseMessage response = Json(Deployment(name, _deploymentBody, _state).ToJsonString(), HttpStatusCode.Created);
                response.Headers.TryAddWithoutValidation("Azure-AsyncOperation",
                    $"https://management.azure.com{DeploymentsPath}{name}/operationStatuses/{OperationId}?api-version=2025-04-01");
                return response;
            }

            if (request.Method == HttpMethod.Delete)
            {
                _deleted = true;
                _pollingDeletion = true;
                return Accepted();
            }

            if (request.Method == HttpMethod.Get && path.Contains("/operationStatuses/", StringComparison.OrdinalIgnoreCase))
            {
                _state = "Succeeded";
                return Json("""{"status":"Succeeded"}""");
            }

            if (request.Method == HttpMethod.Get && path.StartsWith(OperationResultsPath, StringComparison.OrdinalIgnoreCase))
            {
                return _pollingDeletion
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : Json((_whatIfResult ?? throw new InvalidOperationException("No what-if operation was submitted.")).ToJsonString());
            }

            if (request.Method == HttpMethod.Get &&
                path.Equals("/providers/Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new JsonObject
                {
                    ["value"] = _deleted ? new JsonArray() : new JsonArray(Deployment(deploymentName, _deploymentBody, _state)),
                }.ToJsonString());
            }

            if (request.Method == HttpMethod.Get && path.StartsWith(DeploymentsPath, StringComparison.OrdinalIgnoreCase))
            {
                if (_state == "Accepted")
                {
                    _state = "Running";
                }
                return _deleted
                    ? Json("""{"error":{"code":"NotFound"}}""", HttpStatusCode.NotFound)
                    : Json(Deployment(name, _deploymentBody, _state).ToJsonString());
            }

            throw new InvalidOperationException($"Unexpected fake tenant deployment request: {request.Method} {request.RequestUri}");
        }

        private static async Task<JsonObject> ReadBody(HttpRequestMessage request, CancellationToken cancellationToken) =>
            JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();

        private static string ResourceGroupId(JsonObject body) =>
            $"/subscriptions/{body["properties"]!["parameters"]!["nestedSubId"]!["value"]!.GetValue<string>()}/resourceGroups/{body["properties"]!["parameters"]!["nestedRG"]!["value"]!.GetValue<string>()}";

        private static JsonObject Deployment(string name, JsonObject? body, string state, bool validated = false)
        {
            var parameters = new JsonObject();
            if (body?["properties"]?["parameters"] is JsonObject inputs)
            {
                foreach ((string key, JsonNode? value) in inputs)
                {
                    parameters[key] = new JsonObject { ["type"] = "String", ["value"] = value!["value"]?.DeepClone() };
                }
            }
            var properties = new JsonObject
            {
                ["parameters"] = parameters,
                ["extensions"] = new JsonArray(),
                ["mode"] = body?["properties"]?["mode"]?.GetValue<string>() ?? "Incremental",
                ["provisioningState"] = state,
                ["correlationId"] = SubscriptionId,
                ["providers"] = new JsonArray(),
                ["dependencies"] = new JsonArray(),
            };
            if (body?["properties"]?["parameters"]?["nestedRG"] is not null && state == "Succeeded")
            {
                var resources = new JsonArray();
                if (validated)
                {
                    string subscription = body["properties"]!["parameters"]!["nestedSubId"]!["value"]!.GetValue<string>();
                    resources.Add(new JsonObject { ["id"] = $"/subscriptions/{subscription}/providers/Microsoft.Resources/deployments/sub-nested" });
                }
                resources.Add(new JsonObject { ["id"] = ResourceGroupId(body) });
                properties[validated ? "validatedResources" : "outputResources"] = resources;
            }
            if (body?["properties"]?["parameters"]?["nestedRG"] is not null)
            {
                properties["providers"] = new JsonArray(new JsonObject
                {
                    ["namespace"] = "Microsoft.Resources",
                    ["resourceTypes"] = new JsonArray(new JsonObject
                    {
                        ["resourceType"] = "deployments",
                        ["locations"] = new JsonArray("eastus2"),
                    }),
                });
            }
            if (state == "Accepted")
            {
                properties.Remove("extensions");
            }
            return new JsonObject
            {
                ["id"] = DeploymentsPath + name,
                ["name"] = name,
                ["type"] = "Microsoft.Resources/deployments",
                ["location"] = (body?["location"]?.GetValue<string>() ?? Location).ToLowerInvariant(),
                ["properties"] = properties,
            };
        }

        private static HttpResponseMessage Accepted()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.TryAddWithoutValidation("Location", OperationResultsUri);
            response.Headers.TryAddWithoutValidation("Retry-After", "0");
            return response;
        }

        private static HttpResponseMessage Json(
            string body,
            HttpStatusCode statusCode = HttpStatusCode.OK) =>
            new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }
}
