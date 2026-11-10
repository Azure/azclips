using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Runs <c>deployment group</c> through the real dispatcher, binders, and ARM pipeline in one
/// recorded session and one resource group. The steps are validate, what-if, create, show, list,
/// export, the cancel rejections, delete, and cancelling an in-flight deployment. Record mode
/// captures the CLI workflow against Azure. Playback replays the same traffic through the CLI and
/// PowerShell surfaces and matches request bodies, so both surfaces must bind their arguments into
/// the same recorded requests. The commands pass non-default values for typed template
/// parameters, Complete mode, rollback, validation level, template and parameters links with a
/// query string, and result format, so the test covers the binders beyond their defaults.
/// </summary>
[Collection("Scenario Tests")]
public sealed class DeploymentGroupLifecycleScenarioTests
{
    // A method can't share its class's name, so the recording is named explicitly.
    private const string RecordingName = nameof(DeploymentGroupLifecycleScenarioTests);
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-deployment-lifecycle-rg";
    private const string MissingResourceGroupName = "scenario-deployment-missing-rg";
    // Fixed so the validate URI matches on replay; without --name the command names the
    // deployment after the temp template file, which differs on every run.
    private const string DeploymentName = "scenario-deployment";
    private const string BaselineDeploymentName = "scenario-baseline-deployment";
    private const string MissingDeploymentName = "scenario-missing-deployment";
    private const string SlowDeploymentName = "scenario-slow-deployment";
    private const string Location = "westus2";
    private const string DeploymentsUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}/providers/Microsoft.Resources/deployments/";
    private const string EmptyTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"p1":{"type":"string"},"count":{"type":"int"},"withDefault":{"type":"string","defaultValue":"d"}},"resources":[]}""";
    private static readonly string[] s_templateParameters = ["p1=v1", "count=3"];

    // What-if links a public quickstart template, pinned to a commit so a re-record sees the same
    // template. ARM fetches both links itself and appends the query string to each.
    private const string QuickstartUri =
        "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/c56e67ea694a56acb347e3fe6038d047d5d17e44/quickstarts/microsoft.storage/storage-account-create/";
    private const string LinkedTemplateUri = QuickstartUri + "azuredeploy.json";
    private const string LinkedParametersUri = QuickstartUri + "azuredeploy.parameters.json";

    // A storage account takes long enough to provision that the deployment is still cancellable.
    // The account name derives from the resource group id, so no run id appears in the request.
    private const string SlowDeploymentBody =
        """{"properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Storage/storageAccounts","apiVersion":"2023-01-01","name":"[concat('scenario', uniqueString(resourceGroup().id))]","location":"[resourceGroup().location]","sku":{"name":"Standard_LRS"},"kind":"StorageV2"}]}}}""";

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourceGroupDeployments, ExtensionUnderTest.Rest];

    [Fact]
    public void ValidateWhatIfCreateShowListExportCancelDeleteDeployment()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(DeploymentGroupLifecycleScenarioTests), RecordingName, new DefaultRequestMatcher(matchBody: true));
        string template = Path.GetTempFileName();
        File.WriteAllText(template, EmptyTemplate);

        try
        {
            if (recording.Mode == RecorderMode.Record)
            {
                var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
                using var host = new ScenarioHost(
                    ClientType.CLI,
                    recording.CreateInterceptor(),
                    useLiveAuth: true,
                    extensions: s_extensions);
                string subscriptionId = host.LiveSubscriptionId();

                try
                {
                    Lifecycle(new Target(host, ClientType.CLI, subscriptionId, groupName), template, live: true);
                    AssertRequestBodies(recording.Record);
                    recording.Save();
                }
                finally
                {
                    // Runs after the save, so the recording never includes the cleanup.
                    try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
                }
                return;
            }

            // Body matching already proves each surface sent exactly the recorded requests. These
            // assertions check the values inside those requests.
            AssertRequestBodies(recording.Record);

            foreach (var surface in new[] { ClientType.CLI, ClientType.PowerShell })
            {
                using var host = new ScenarioHost(surface, recording.CreateInterceptor(), extensions: s_extensions);
                Lifecycle(new Target(host, surface, SubscriptionId, ResourceGroupName), template, live: false);
            }
        }
        finally
        {
            File.Delete(template);
        }
    }

    // Without --name the command names the deployment after the template file. This test
    // returns a canned ARM response instead of calling Azure.
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateWithoutName_PostsToTemplateFileName(ClientType surface)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string template = Path.Combine(directory, "main.json");
        File.WriteAllText(template, EmptyTemplate);
        try
        {
            var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$$"""{"properties":{"provisioningState":"{{{State.Succeeded}}}"}}""", Encoding.UTF8, "application/json"),
            });
            using (var host = new ScenarioHost(surface, http, extensions: s_extensions))
            {
                Validate(new Target(host, surface, SubscriptionId, ResourceGroupName), template, deploymentName: null);
            }

            var request = http.Captured[0];
            Assert.Equal("POST", request.Method);
            Assert.Equal(DeploymentsUri + "main/validate?api-version=2025-04-01", request.Uri);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ARM validate results under Location are one-shot: re-reading the terminal result can answer
    // 404. Validation (and create's preflight) must stop polling at the first non-202.
    [Theory]
    [InlineData(ClientType.CLI, false)]
    [InlineData(ClientType.PowerShell, false)]
    [InlineData(ClientType.CLI, true)]
    [InlineData(ClientType.PowerShell, true)]
    public void Validate_PollsLocationWithoutExtraFinalGet(ClientType surface, bool create)
    {
        const string OperationResults = "https://management.azure.com/operationResults/";
        const string L1 = OperationResults + "l1";
        const string L2 = OperationResults + "l2";
        string template = Path.GetTempFileName();
        File.WriteAllText(template, EmptyTemplate);
        try
        {
            var http = new CapturingTracingInterceptor(request =>
            {
                static HttpResponseMessage Accepted(string location)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Accepted);
                    response.Headers.Location = new Uri(location);
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                    return response;
                }

                string uri = request.RequestUri!.AbsoluteUri;
                string ok = $$$"""{"properties":{"provisioningState":"{{{State.Succeeded}}}"}}""";
                return (request.Method.Method, uri) switch
                {
                    ("POST", _) => Accepted(L1),
                    ("GET", L1) => Accepted(L2),
                    ("GET", L2) or ("PUT", _) or ("GET", DeploymentsUri + DeploymentName + "?api-version=2025-04-01") =>
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ok, Encoding.UTF8, "application/json") },
                    _ => new HttpResponseMessage(HttpStatusCode.NotFound),
                };
            });
            using (var host = new ScenarioHost(surface, http, extensions: s_extensions))
            {
                var target = new Target(host, surface, SubscriptionId, ResourceGroupName);
                _ = create ? CreateDeployment(target, DeploymentName, template, rollbackOnError: false) : Validate(target, template, DeploymentName);
            }

            // Create's own PUT (and its LRO GET) follows; only the validate traffic matters here.
            string[] expected = ["POST", "GET " + L1, "GET " + L2];
            Assert.Equal(expected, http.Captured.Where(r => r.Method == "POST" || r.Uri.StartsWith(OperationResults)).Select(r => r.Method == "GET" ? $"GET {r.Uri}" : r.Method));
            Assert.Equal(create, http.Captured.Any(r => r.Method == "PUT"));
        }
        finally
        {
            File.Delete(template);
        }
    }

    // One surface pointed at one resource group; Run picks the CLI or PowerShell form of a command.
    private sealed record Target(ScenarioHost Host, ClientType Surface, string SubscriptionId, string GroupName)
    {
        public IReadOnlyList<object> Run(string[] cli, string cmdlet, Dictionary<string, object> powerShell) =>
            Surface == ClientType.CLI ? Host.RunCli(cli) : Host.RunPowerShell(cmdlet, powerShell);
    }

    // ARM deployment provisioning states this scenario asserts on.
    private static class State
    {
        public const string Accepted = "Accepted";
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string Canceled = "Canceled";
    }

    private static void Lifecycle(Target target, string template, bool live)
    {
        Assert.IsType<ResourceGroup>(Assert.Single(CreateGroup(target)));

        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(Validate(target, template, DeploymentName)));
        Assert.Null(validation.Error);
        Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);

        var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(WhatIf(target)));
        Assert.Equal(State.Succeeded, whatIf.Status);
        Assert.Null(whatIf.Error);
        // The linked quickstart template adds one storage account.
        var change = Assert.Single(whatIf.Properties?.Changes ?? []);
        Assert.Equal("Create", change.ChangeType);
        Assert.Contains("Microsoft.Storage/storageAccounts", change.ResourceId ?? "", StringComparison.OrdinalIgnoreCase);

        AssertMissingTemplateParameterFails(target, template);

        // ARM only accepts a LastSuccessful rollback when another deployment in the group has
        // succeeded, so deploy a baseline without rollback first.
        foreach (var (name, rollbackOnError) in new[] { (BaselineDeploymentName, false), (DeploymentName, true) })
        {
            var created = Assert.IsType<DeploymentExtended>(Assert.Single(CreateDeployment(target, name, template, rollbackOnError)));
            Assert.Equal(name, created.Name);
            Assert.Equal(State.Succeeded, created.Properties?.ProvisioningState);
        }

        var shown = Assert.IsType<DeploymentExtended>(Assert.Single(Show(target, DeploymentName)));
        Assert.Equal(DeploymentName, shown.Name);
        Assert.Equal(State.Succeeded, shown.Properties?.ProvisioningState);

        // The list command writes every page as one collected List<DeploymentExtended>.
        var deployments = Assert.IsType<List<DeploymentExtended>>(Assert.Single(List(target)));
        Assert.Equal(
            [BaselineDeploymentName, DeploymentName],
            deployments.Select(d => d.Name).Order(StringComparer.Ordinal));
        Assert.All(deployments, d => Assert.Equal(State.Succeeded, d.Properties?.ProvisioningState));

        AssertExportsEmptyTemplate(target);

        AssertCancelFails("DeploymentCannotBeCancelled", target, DeploymentName);
        AssertCancelFails("DeploymentNotFound", target, MissingDeploymentName);
        AssertCancelFails("ResourceGroupNotFound", target with { GroupName = MissingResourceGroupName }, DeploymentName);

        Assert.Empty(Delete(target, BaselineDeploymentName));
        Assert.Empty(Delete(target, DeploymentName));

        Assert.Empty(Assert.IsType<List<DeploymentExtended>>(Assert.Single(List(target))));

        CancelInFlightDeployment(target, live);
    }

    // CLI `deployment group export` prints the template; PowerShell
    // `Save-AzResourceGroupDeploymentTemplate` writes it to a file.
    private static void AssertExportsEmptyTemplate(Target target)
    {
        if (target.Surface == ClientType.CLI)
        {
            AssertEmptyTemplate(Assert.IsType<JsonElement>(Assert.Single(target.Host.RunCli(
                "deployment", "group", "export",
                "--subscription-id", target.SubscriptionId,
                "--resource-group", target.GroupName,
                "--name", DeploymentName))));
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"azclips-export-{Guid.NewGuid():N}.json");
        try
        {
            var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(target.Host.RunPowerShell(
                "Save-AzResourceGroupDeploymentTemplate",
                new Dictionary<string, object>
                {
                    ["SubscriptionId"] = target.SubscriptionId,
                    ["ResourceGroupName"] = target.GroupName,
                    ["Name"] = DeploymentName,
                    ["Path"] = path,
                    ["Force"] = true,
                })));
            Assert.Equal(path, saved.Path);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            AssertEmptyTemplate(document.RootElement);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertEmptyTemplate(JsonElement template)
    {
        Assert.True(template.TryGetProperty("$schema", out _), $"Exported template has no $schema: {template}");
        Assert.Equal(0, template.GetProperty("resources").GetArrayLength());
    }

    private static void CancelInFlightDeployment(Target target, bool live)
    {
        // `deployment group create` blocks until the deployment is terminal, so start the
        // deployment to cancel with a raw PUT that returns as soon as ARM accepts it.
        var response = Assert.IsType<RestResponse>(Assert.Single(StartSlowDeployment(target)));
        Assert.Equal(201, response.StatusCode);

        string? state = WaitForState(target, live, attempts: 15, State.Accepted, State.Running);
        Assert.True(state is State.Accepted or State.Running, $"The deployment to cancel is '{state}', not cancellable; re-record this test.");

        try
        {
            Assert.Empty(Cancel(target, SlowDeploymentName));
        }
        catch (HttpRequestException error) when (error.Message.Contains("DeploymentCannotBeCancelled"))
        {
            Assert.Fail("The deployment finished before it could be cancelled; re-record this test.");
        }

        Assert.Equal(State.Canceled, WaitForState(target, live, attempts: 30, State.Canceled));
    }

    private static void AssertCancelFails(string expectedCode, Target target, string deploymentName)
    {
        var error = Assert.Throws<HttpRequestException>(() => Cancel(target, deploymentName));
        Assert.Contains(expectedCode, error.Message);
    }

    // Stops at a wanted state or any terminal one. Playback replays the recorded responses in
    // order, so it takes the same number of polls; only record mode waits between them.
    private static string? WaitForState(Target target, bool live, int attempts, params string[] wanted)
    {
        string? state = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(Show(target, SlowDeploymentName)));
            state = deployment.Properties?.ProvisioningState;
            if (wanted.Contains(state) || state is State.Succeeded or State.Failed or State.Canceled)
            {
                break;
            }

            if (live)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
        return state;
    }

    // A required template parameter missing from the command fails before the command sends a
    // request, so no request reaches ARM or the recording.
    private static void AssertMissingTemplateParameterFails(Target target, string template)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() => target.Run(
            [
                "deployment", "group", "create",
                "--subscription-id", target.SubscriptionId,
                "--resource-group", target.GroupName,
                "--name", DeploymentName,
                "--template-file", template,
                "--parameter", "p1=v1",
            ],
            "New-AzResourceGroupDeployment",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
                ["Name"] = DeploymentName,
                ["TemplateFile"] = template,
                ["Parameter"] = new[] { "p1=v1" },
            }));
        Assert.Contains("count", error.Message);
    }

    private static void AssertRequestBodies(SessionRecord record)
    {
        var validate = FirstRequestProperties(record, "POST", $"/deployments/{DeploymentName}/validate");
        AssertTypedParameters(validate);
        Assert.False(HasValue(validate, "onErrorDeployment"), "validate was sent without rollback, so it carries no onErrorDeployment.");

        var whatIf = FirstRequestProperties(record, "POST", $"/deployments/{DeploymentName}/whatIf");
        Assert.Equal("Incremental", whatIf.GetProperty("mode").GetString());
        var templateLink = whatIf.GetProperty("templateLink");
        Assert.Equal(LinkedTemplateUri, templateLink.GetProperty("uri").GetString());
        Assert.Equal("sig=abc", templateLink.GetProperty("queryString").GetString());
        Assert.Equal(LinkedParametersUri, whatIf.GetProperty("parametersLink").GetProperty("uri").GetString());
        Assert.False(HasValue(whatIf, "template"), "A linked what-if carries no inline template.");
        Assert.Equal("ResourceIdOnly", whatIf.GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
        Assert.Equal("Provider", whatIf.GetProperty("validationLevel").GetString());

        var baseline = Assert.Single(Requests(record, "PUT", $"/deployments/{BaselineDeploymentName}?"));
        var create = Assert.Single(Requests(record, "PUT", $"/deployments/{DeploymentName}?"));
        foreach (var put in new[] { baseline, create })
        {
            Assert.Equal("Complete", put.GetProperty("mode").GetString());
            Assert.True(put.GetProperty("template").TryGetProperty("resources", out _));
            AssertTypedParameters(put);
            Assert.Equal("Provider", put.GetProperty("validationLevel").GetString());
        }
        Assert.False(HasValue(baseline, "onErrorDeployment"), "The baseline create was sent without rollback.");
        Assert.Equal("LastSuccessful", create.GetProperty("onErrorDeployment").GetProperty("type").GetString());
    }

    private static void AssertTypedParameters(JsonElement properties)
    {
        var parameters = properties.GetProperty("parameters");
        Assert.Equal("v1", parameters.GetProperty("p1").GetProperty("value").GetString());
        Assert.Equal(3, parameters.GetProperty("count").GetProperty("value").GetInt32());
    }

    // Returns the properties of every recorded request that matches the method and URI, in request order.
    private static List<JsonElement> Requests(SessionRecord record, string method, string uriPart) =>
        [.. record.Entries
            .Where(e => e.Request.Method == method && e.Request.Uri.Contains(uriPart, StringComparison.OrdinalIgnoreCase))
            .Select(e =>
            {
                Assert.NotNull(e.Request.Body);
                using var document = JsonDocument.Parse(e.Request.Body);
                return document.RootElement.GetProperty("properties").Clone();
            })];

    private static JsonElement FirstRequestProperties(SessionRecord record, string method, string uriPart)
    {
        var requests = Requests(record, method, uriPart);
        Assert.True(requests.Count > 0, $"No recorded {method} request matching '{uriPart}'.");
        return requests[0];
    }

    private static bool HasValue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null;

    private static IReadOnlyList<object> CreateGroup(Target target) =>
        target.Run(
            ["group", "create", "--subscription-id", target.SubscriptionId, "--name", target.GroupName, "--location", Location],
            "New-AzResourceGroup",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["Name"] = target.GroupName,
                ["Location"] = Location,
            });

    // A null deploymentName omits --name, so the command generates a GUID name itself.
    private static IReadOnlyList<object> Validate(Target target, string template, string? deploymentName)
    {
        var powerShell = new Dictionary<string, object>
        {
            ["SubscriptionId"] = target.SubscriptionId,
            ["ResourceGroupName"] = target.GroupName,
            ["TemplateFile"] = template,
            ["Parameter"] = s_templateParameters,
        };
        if (deploymentName is not null)
        {
            powerShell["Name"] = deploymentName;
        }

        return target.Run(
            [
                "deployment", "group", "validate",
                "--subscription-id", target.SubscriptionId,
                "--resource-group", target.GroupName,
                .. (deploymentName is null ? [] : new[] { "--name", deploymentName }),
                "--template-file", template,
                "--parameter", s_templateParameters[0],
                "--parameter", s_templateParameters[1],
            ],
            "Test-AzResourceGroupDeployment",
            powerShell);
    }

    private static IReadOnlyList<object> WhatIf(Target target) =>
        target.Run(
            [
                "deployment", "group", "what-if",
                "--subscription-id", target.SubscriptionId,
                "--resource-group", target.GroupName,
                "--name", DeploymentName,
                "--template-uri", LinkedTemplateUri,
                "--query-string", "?sig=abc",
                "--parameters-uri", LinkedParametersUri,
                "--result-format", "ResourceIdOnly",
                "--validation-level", "Provider",
            ],
            "New-AzResourceGroupDeploymentWhatIf",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
                ["Name"] = DeploymentName,
                ["TemplateUri"] = LinkedTemplateUri,
                ["QueryString"] = "?sig=abc",
                ["ParametersUri"] = LinkedParametersUri,
                ["ResultFormat"] = "ResourceIdOnly",
                ["ValidationLevel"] = "Provider",
            });

    private static IReadOnlyList<object> CreateDeployment(Target target, string deploymentName, string template, bool rollbackOnError) =>
        target.Run(
            [
                "deployment", "group", "create",
                "--subscription-id", target.SubscriptionId,
                "--resource-group", target.GroupName,
                "--name", deploymentName,
                "--template-file", template,
                "--parameter", s_templateParameters[0],
                "--parameter", s_templateParameters[1],
                "--mode", "Complete",
                "--validation-level", "Provider",
                .. (rollbackOnError ? new[] { "--rollback-on-error" } : []),
            ],
            "New-AzResourceGroupDeployment",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
                ["Name"] = deploymentName,
                ["TemplateFile"] = template,
                ["Parameter"] = s_templateParameters,
                ["Mode"] = "Complete",
                ["RollbackOnError"] = rollbackOnError,
                ["ValidationLevel"] = "Provider",
            });

    private static IReadOnlyList<object> Show(Target target, string deploymentName) =>
        target.Run(
            ["deployment", "group", "show", "--subscription-id", target.SubscriptionId, "--resource-group", target.GroupName, "--name", deploymentName],
            "Get-AzResourceGroupDeployment_Get",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
                ["Name"] = deploymentName,
            });

    private static IReadOnlyList<object> List(Target target) =>
        target.Run(
            ["deployment", "group", "list", "--subscription-id", target.SubscriptionId, "--resource-group", target.GroupName],
            "Get-AzResourceGroupDeployment_List",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
            });

    private static IReadOnlyList<object> Delete(Target target, string deploymentName) =>
        target.Run(
            ["deployment", "group", "delete", "--subscription-id", target.SubscriptionId, "--resource-group", target.GroupName, "--name", deploymentName],
            "Remove-AzResourceGroupDeployment",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
                ["Name"] = deploymentName,
            });

    private static IReadOnlyList<object> StartSlowDeployment(Target target)
    {
        string uri = $"https://management.azure.com/subscriptions/{target.SubscriptionId}/resourcegroups/{target.GroupName}/providers/Microsoft.Resources/deployments/{SlowDeploymentName}?api-version=2025-04-01";
        return target.Run(
            ["rest", "--method", "PUT", "--uri", uri, "--body", SlowDeploymentBody],
            "Invoke-AzRestMethod",
            new()
            {
                ["Method"] = "PUT",
                ["Uri"] = uri,
                ["Payload"] = SlowDeploymentBody,
            });
    }

    private static IReadOnlyList<object> Cancel(Target target, string deploymentName) =>
        target.Run(
            ["deployment", "group", "cancel", "--subscription-id", target.SubscriptionId, "--resource-group", target.GroupName, "--name", deploymentName],
            "Stop-AzResourceGroupDeployment",
            new()
            {
                ["SubscriptionId"] = target.SubscriptionId,
                ["ResourceGroupName"] = target.GroupName,
                ["Name"] = deploymentName,
            });
}