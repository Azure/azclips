// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Tenant-scope deployment scenarios derived from Azure CLI and Azure PowerShell tests. Each test has its own recording,
/// template file, run-scoped names and sanitizers. Record mode runs the CLI surface against live Azure; playback
/// replays the recording through the CLI and public PowerShell cmdlets with body matching.
/// Offline cmdlet regressions use an in-memory backend and never access Azure.
/// <para>
/// Recording needs:
/// <list type="bullet">
/// <item><see cref="TenantDeploymentLifecycle"/> (T1): <c>Microsoft.Resources/deployments/write</c> at the tenant root
/// (<c>/</c>) and resource-group create rights in the signed-in subscription.</item>
/// <item><see cref="TenantDeploymentWhatIf"/> (T2): permission to run what-if at the tenant root (<c>/</c>).</item>
/// <item><see cref="TenantDeploymentFromTemplateSpecWithParametersFile"/> (T3): tenant deployment rights and
/// resource-group and Template Spec create/delete rights in the signed-in subscription.</item>
/// <item>T4 and T5: the same what-if permissions as T2; neither creates resources.</item>
/// </list>
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public sealed class TenantDeploymentScenarioTests : IDisposable
{
    // Playback sends the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = RecordingSanitizers.PlaceholderGuid;
    private const string Location = "WestUS";
    private const string ApiVersion = "api-version=2025-04-01";
    private const string DeploymentsUri = "https://management.azure.com/providers/Microsoft.Resources/deployments";
    private const string SucceededFilter = "provisioningState eq 'Succeeded'";
    private const string RunningCondition = "properties.provisioningState=='Running'";
    private const string CanceledCondition = "properties.provisioningState=='Canceled'";
    private const string WaitTimeout = "300";

    // Each wait polls every second for at most 300 seconds, so PowerShell replays at most this many recorded GETs.
    private const int MaxWaitPolls = 300;

    private const string DeploymentBaseName = "scenario-tenant-deployment";
    private const string CancelDeploymentBaseName = "scenario-tenant-deployment-cancel";
    // Session-record entries carry base names, not the run-scoped ones.
    private const string CancelDeploymentRecordedUri = $"{DeploymentsUri}/{CancelDeploymentBaseName}?{ApiVersion}";
    private const string ResourceGroupBaseName = "scenario-tenant-deployment-rg";
    private const string WhatIfDeploymentBaseName = "scenario-tenant-what-if";
    private const string WhatIfResourceGroupBaseName = "scenario-tenant-what-if-rg";
    private const string TemplateSpecDeploymentBaseName = "scenario-tenant-template-spec";
    private const string TemplateSpecResourceGroupBaseName = "scenario-tenant-template-spec-rg";
    private const string TemplateSpecApiVersion = "api-version=2022-02-01";
    private const string TemplateSpecParameters = """{"foo":{"value":"abc"},"bar":{"value":"xyz"}}""";
    private const string TemplateSpecTemplate = """
        {
          "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "parameters": {
            "foo": { "type": "string" },
            "bar": { "type": "string" }
          },
          "resources": [],
          "outputs": {
            "foo": { "type": "string", "value": "[parameters('foo')]" },
            "bar": { "type": "string", "value": "[parameters('bar')]" }
          }
        }
        """;

    // The only resource is a nested subscription deployment that creates one resource group, so the scenarios need no
    // management group and no tenant-level role or policy. The trailing comment is dropped from the request body.
    private const string Template = """
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
              "name": "[parameters('nestedRG')]",
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
          // The deployment only creates a resource group in the subscription.
        }
        """;

    // Template plus a serial chain of empty resource-group deployments, so the deployment the lifecycle cancels is
    // still running when the cancel arrives. It creates nothing billable; the chain's history lives in the resource
    // group the cleanup deletes.
    private const string SlowTemplate = """
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
              "name": "[parameters('nestedRG')]",
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
            },
            {
              "type": "Microsoft.Resources/deployments",
              "apiVersion": "2021-04-01",
              "name": "[format('slow-{0}', copyIndex())]",
              "subscriptionId": "[parameters('nestedSubId')]",
              "resourceGroup": "[parameters('nestedRG')]",
              "dependsOn": [ "[parameters('nestedRG')]" ],
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
          ]
        }
        """;

    private static readonly ExtensionUnderTest[] s_lifecycleExtensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments];

    private static readonly ExtensionUnderTest[] s_whatIfExtensions = [ExtensionUnderTest.ResourcesDeployments];
    private static readonly ExtensionUnderTest[] s_templateSpecExtensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments, ExtensionUnderTest.Rest];

    private readonly RecorderMode _mode = RecorderModeResolver.FromEnvironment();

    private readonly string _templateFile = Path.GetTempFileName();

    // xUnit creates one instance per test, so each test writes and deletes its own template file.
    public TenantDeploymentScenarioTests() => File.WriteAllText(_templateFile, Template);

    public void Dispose() => File.Delete(_templateFile);

    // Record mode adds a run-scoped suffix, which the sanitisers strip, so recordings and playback keep the base name.
    private string Deployment => TestRun.ScopedName(DeploymentBaseName, _mode);

    private string CancelDeployment => TestRun.ScopedName(CancelDeploymentBaseName, _mode);

    private string ResourceGroup => TestRun.ScopedName(ResourceGroupBaseName, _mode);

    private string WhatIfDeployment => TestRun.ScopedName(WhatIfDeploymentBaseName, _mode);

    private string WhatIfResourceGroup => TestRun.ScopedName(WhatIfResourceGroupBaseName, _mode);

    private string TemplateSpecDeployment => TestRun.ScopedName(TemplateSpecDeploymentBaseName, _mode);

    private string TemplateSpecResourceGroup => TestRun.ScopedName(TemplateSpecResourceGroupBaseName, _mode);

    // T1: verifies "T1 Tenant deployment lifecycle: validate, create, list, show, export, delete, then cancel and wait on
    // a second deployment" of resources-deployments-tests (requirement "Create validates, then deploys and waits").
    // Upstream: azure-cli test_resource.py DeploymentTestAtTenantScope.test_tenant_level_deployment.
    [Fact]
    public void TenantDeploymentLifecycle()
    {
        using var slowTemplate = new TemplateFile(SlowTemplate);
        var subscriptionSanitizer = new LiveSubscriptionSanitizer();
        var recording = SessionRecordFiles.OpenSession(
            typeof(TenantDeploymentScenarioTests),
            nameof(TenantDeploymentLifecycle),
            new DefaultRequestMatcher(matchBody: true),
            Sanitizers(new TenantDeploymentListSanitizer(Deployment, CancelDeployment), subscriptionSanitizer));

        if (recording.Mode == RecorderMode.Record)
        {
            RecordPlaybackInterceptor requests = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, requests, useLiveAuth: true, extensions: s_lifecycleExtensions);
            string liveSubscriptionId = host.LiveSubscriptionId();
            subscriptionSanitizer.SubscriptionId = liveSubscriptionId;

            bool workflowSucceeded = false;
            try
            {
                RunCliLifecycle(host, requests, recording.Record, liveSubscriptionId, slowTemplate.Path);
                recording.Save();
                workflowSucceeded = true;
            }
            finally
            {
                // After the save, so cleanup never lands in the recording. Every item is tried, a resource that is
                // already gone counts as cleaned up, and a cleanup failure never replaces a workflow failure.
                var failures = new List<Exception>();
                TryCleanUp(failures, "tenant deployment", CancelDeployment,
                    () => host.RunCli("deployment", "tenant", "delete", "--name", CancelDeployment));
                TryCleanUp(failures, "tenant deployment", Deployment,
                    () => host.RunCli("deployment", "tenant", "delete", "--name", Deployment));
                TryCleanUp(failures, "resource group", ResourceGroup,
                    () => host.RunCli("group", "delete", "--subscription-id", liveSubscriptionId, "--name", ResourceGroup));
                if (workflowSucceeded && failures.Count > 0)
                {
                    throw new AggregateException("Record-mode cleanup failed; delete the named resources manually.", failures);
                }
            }
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliRequests, extensions: s_lifecycleExtensions))
        {
            RunCliLifecycle(cli, cliRequests, recording.Record, SubscriptionId, slowTemplate.Path);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_lifecycleExtensions))
        {
            RunPowerShellLifecycle(ps, slowTemplate.Path);
        }
    }

    // T2/T4/T5: default, ResourceIdOnly and ProviderNoRbac what-if share one recording.
    // Upstream: azure-cli test_resource.py DeploymentWhatIfAtTenantScopeTest.test_tenant_level_what_if.
    // PowerShell: DeploymentWhatIfTests.ps1 Test-WhatIfWithResourceIdOnlyAtTenantScope and Test-WhatIfProviderNoRbacAtTenantScope.
    [Fact]
    public void TenantDeploymentWhatIf()
    {
        var subscriptionSanitizer = new LiveSubscriptionSanitizer();
        var recording = SessionRecordFiles.OpenSession(
            typeof(TenantDeploymentScenarioTests),
            nameof(TenantDeploymentWhatIf),
            new DefaultRequestMatcher(matchBody: true),
            Sanitizers(subscriptionSanitizer));

        if (recording.Mode == RecorderMode.Record)
        {
            // What-if creates nothing, so there is no cleanup.
            RecordPlaybackInterceptor requests = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, requests, useLiveAuth: true, extensions: s_whatIfExtensions);
            string liveSubscriptionId = host.LiveSubscriptionId();
            subscriptionSanitizer.SubscriptionId = liveSubscriptionId;
            RunWhatIfWorkflow(host, requests, liveSubscriptionId, powerShell: false);
            recording.Save();
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliRequests, extensions: s_whatIfExtensions))
        {
            RunWhatIfWorkflow(cli, cliRequests, SubscriptionId, powerShell: false);
        }

        RecordPlaybackInterceptor psRequests = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(ClientType.PowerShell, psRequests, extensions: s_whatIfExtensions))
        {
            RunWhatIfWorkflow(ps, psRequests, SubscriptionId, powerShell: true);
        }
    }

    private void RunWhatIfWorkflow(ScenarioHost host, RecordPlaybackInterceptor requests, string subscriptionId, bool powerShell)
    {
        foreach (var (resultFormat, validationLevel) in new (string?, string?)[]
        {
            (null, null),
            ("ResourceIdOnly", null),
            (null, "ProviderNoRbac"),
        })
        {
            if (!powerShell)
            {
                RunCliWhatIf(host, requests, subscriptionId, resultFormat, validationLevel);
                continue;
            }

            Dictionary<string, object> parameters = DeployParameters(WhatIfDeployment, subscriptionId, WhatIfResourceGroup);
            if (resultFormat is not null)
            {
                parameters["ResultFormat"] = resultFormat;
            }
            if (validationLevel is not null)
            {
                parameters["ValidationLevel"] = validationLevel;
            }

            int sent = requests.Sent.Count;
            AssertWhatIfResult(host.RunPowerShellCmdlet<GetTenantDeploymentWhatIfResultCmdlet>(parameters), resultFormat);
            AssertWhatIfRequest(Since(requests, sent)[0], resultFormat, validationLevel);
        }
    }

    // T3: upstream DeploymentTests.ps1 Test-NewTenantDeploymentFromTemplateSpec.
    [Fact]
    public void TenantDeploymentFromTemplateSpecWithParametersFile()
    {
        using var parametersFile = new TemplateFile(TemplateSpecParameters);
        var subscriptionSanitizer = new LiveSubscriptionSanitizer();
        var recording = SessionRecordFiles.OpenSession(
            typeof(TenantDeploymentScenarioTests),
            nameof(TenantDeploymentFromTemplateSpecWithParametersFile),
            new DefaultRequestMatcher(matchBody: true),
            Sanitizers(subscriptionSanitizer));

        if (recording.Mode == RecorderMode.Record)
        {
            RecordPlaybackInterceptor requests = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, requests, useLiveAuth: true, extensions: s_templateSpecExtensions);
            string subscriptionId = host.LiveSubscriptionId();
            subscriptionSanitizer.SubscriptionId = subscriptionId;
            bool workflowSucceeded = false;
            try
            {
                RunTemplateSpecDeployment(host, requests, subscriptionId, parametersFile.Path, powerShell: false);
                recording.Save();
                workflowSucceeded = true;
            }
            finally
            {
                var failures = new List<Exception>();
                TryCleanUp(failures, "tenant deployment", TemplateSpecDeployment,
                    () => host.RunCli("deployment", "tenant", "delete", "--name", TemplateSpecDeployment));
                TryCleanUp(failures, "resource group", TemplateSpecResourceGroup,
                    () => host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", TemplateSpecResourceGroup));
                if (workflowSucceeded && failures.Count > 0)
                {
                    throw new AggregateException("Record-mode cleanup failed; delete the named resources manually.", failures);
                }
            }
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliRequests, extensions: s_templateSpecExtensions))
        {
            RunTemplateSpecDeployment(cli, cliRequests, SubscriptionId, parametersFile.Path, powerShell: false);
        }

        RecordPlaybackInterceptor psRequests = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(ClientType.PowerShell, psRequests, extensions: s_templateSpecExtensions))
        {
            RunTemplateSpecDeployment(ps, psRequests, SubscriptionId, parametersFile.Path, powerShell: true);
        }
    }

    private void RunTemplateSpecDeployment(
        ScenarioHost host, RecordPlaybackInterceptor requests, string subscriptionId, string parametersFile, bool powerShell)
    {
        IReadOnlyList<object> groupOutput = powerShell
            ? host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["Name"] = TemplateSpecResourceGroup,
                ["Location"] = Location,
            })
            : host.RunCli("group", "create", "--name", TemplateSpecResourceGroup, "--location", Location);
        var group = Assert.IsType<ResourceGroup>(Assert.Single(groupOutput));
        Assert.Equal(TemplateSpecResourceGroup, group.Name);
        Assert.Equal("Succeeded", group.Properties?.ProvisioningState);

        string specId = $"/subscriptions/{subscriptionId}/resourceGroups/{TemplateSpecResourceGroup}/providers/Microsoft.Resources/templateSpecs/{TemplateSpecDeployment}";
        string versionId = $"{specId}/versions/v1";
        var spec = new JsonObject { ["location"] = Location, ["properties"] = new JsonObject() };
        var version = new JsonObject
        {
            ["location"] = Location,
            ["properties"] = new JsonObject { ["mainTemplate"] = JsonNode.Parse(TemplateSpecTemplate) },
        };
        foreach (var (id, body) in new[] { (specId, spec), (versionId, version) })
        {
            string uri = $"https://management.azure.com{id}?{TemplateSpecApiVersion}";
            int sent = requests.Sent.Count;
            IReadOnlyList<object> output = powerShell
                ? host.RunPowerShell("Invoke-AzRestMethod", new Dictionary<string, object>
                {
                    ["Method"] = "PUT",
                    ["Uri"] = uri,
                    ["Payload"] = body.ToJsonString(),
                })
                : host.RunCli("rest", "--method", "PUT", "--uri", uri, "--body", body.ToJsonString());
            var response = Assert.IsType<RestResponse>(Assert.Single(output));
            Assert.True(response.StatusCode is 200 or 201, $"Template Spec setup returned HTTP {response.StatusCode}.");
            CapturedHttpRequest request = Assert.Single(Since(requests, sent));
            AssertRequest(request, "PUT", uri);
            Assert.True(JsonNode.DeepEquals(body, RequestBody(request)));
        }

        int beforeCreate = requests.Sent.Count;
        IReadOnlyList<object> created = powerShell
            ? host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
            {
                ["Name"] = TemplateSpecDeployment,
                ["Location"] = Location,
                ["TemplateSpecId"] = versionId,
                ["ParametersFile"] = parametersFile,
            })
            : host.RunCli("deployment", "tenant", "create", "--name", TemplateSpecDeployment, "--location", Location,
                "--template-spec-id", versionId, "--parameters-file", parametersFile);
        var deployment = AssertDeployment(created, TemplateSpecDeployment, "Succeeded");
        Assert.NotNull(deployment.Properties?.Outputs);
        Assert.Equal("abc", deployment.Properties.Outputs["foo"].Value);
        Assert.Equal("xyz", deployment.Properties.Outputs["bar"].Value);

        IReadOnlyList<CapturedHttpRequest> createRequests = Since(requests, beforeCreate);
        AssertValidateThenPut(createRequests, TemplateSpecDeployment);
        foreach (CapturedHttpRequest request in createRequests.Where(request => request.Method is "POST" or "PUT"))
        {
            JsonNode body = RequestBody(request);
            Assert.Equal(Location, body["location"]?.GetValue<string>());
            JsonNode properties = body["properties"]!;
            Assert.Equal("Incremental", properties["mode"]?.GetValue<string>());
            Assert.Equal(versionId, properties["templateLink"]?["id"]?.GetValue<string>());
            Assert.Null(properties["template"]);
            Assert.Null(properties["parametersLink"]);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(TemplateSpecParameters), properties["parameters"]));
        }

        Assert.Empty(powerShell
            ? host.RunPowerShellCmdlet<RemoveTenantDeploymentCmdlet>(Named(TemplateSpecDeployment))
            : host.RunCli("deployment", "tenant", "delete", "--name", TemplateSpecDeployment));
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("DeploymentName")]
    [InlineData("Filter")]
    [InlineData("")]
    public void PowerShellCmdlets_BindReadParameters_AndEnumeratePages(string parameter)
    {
        bool list = parameter is "" or "Filter";
        string uri = $"{DeploymentsUri}{(list ? "" : "/" + Deployment)}?{ApiVersion}";
        if (parameter == "Filter")
        {
            uri += $"&$filter={Uri.EscapeDataString(SucceededFilter)}";
        }
        string nextLink = $"{DeploymentsUri}?{ApiVersion}&$skiptoken=second-page";
        var requests = new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord());
        using var backend = new CmdletBackend(request => CmdletResponse(!list
            ? CmdletDeploymentBody(Deployment)
            : request.RequestUri!.AbsoluteUri == nextLink
                ? $$"""{"value":[{{CmdletDeploymentBody("second-deployment")}}]}"""
                : $$"""{"value":[{{CmdletDeploymentBody(Deployment)}}],"nextLink":"{{nextLink}}"}"""));
        using var host = new ScenarioHost(ClientType.PowerShell, requests, backendHandler: backend, extensions: s_whatIfExtensions);
        var parameters = new Dictionary<string, object>();
        if (parameter.Length > 0)
        {
            parameters[parameter] = parameter == "Filter" ? SucceededFilter : Deployment;
        }

        IReadOnlyList<object> output = host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(parameters);

        Assert.Equal(list ? [Deployment, "second-deployment"] : new[] { Deployment },
            output.Select(item => Assert.IsType<DeploymentExtended>(item).Name));
        Assert.Equal(list ? 2 : 1, requests.Sent.Count);
        AssertRequest(requests.Sent[0], "GET", uri);
        if (list)
        {
            AssertRequest(requests.Sent[1], "GET", nextLink);
        }
    }

    [Fact]
    public void PowerShellCmdlets_EmptyList_EnumeratesNoObjects()
    {
        var requests = new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord());
        using var backend = new CmdletBackend(_ => CmdletResponse("""{"value":[]}"""));
        using var host = new ScenarioHost(ClientType.PowerShell, requests, backendHandler: backend, extensions: s_whatIfExtensions);

        Assert.Empty(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(new Dictionary<string, object>()));
        AssertRequest(Assert.Single(requests.Sent), "GET", $"{DeploymentsUri}?{ApiVersion}");
    }

    [Fact]
    public void PowerShellCmdlets_BindDeploymentNameAlias_ParameterArray_AndNoWaitSwitch()
    {
        string specId = $"/subscriptions/{SubscriptionId}/resourceGroups/{TemplateSpecResourceGroup}/providers/Microsoft.Resources/templateSpecs/{TemplateSpecDeployment}/versions/v1";
        var requests = new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord());
        using var backend = new CmdletBackend(request => CmdletResponse(request.Method == HttpMethod.Post
            ? """{"properties":{"provisioningState":"Succeeded"}}"""
            : CmdletDeploymentBody(Deployment, "Accepted")));
        using var host = new ScenarioHost(ClientType.PowerShell, requests, backendHandler: backend, extensions: s_whatIfExtensions);

        var output = host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
        {
            ["DeploymentName"] = Deployment,
            ["Location"] = Location,
            ["TemplateSpecId"] = specId,
            ["Parameter"] = new[] { "message=from-binding", "count=3" },
            ["NoWait"] = new SwitchParameter(true),
        });

        AssertDeployment(output, Deployment, "Accepted");
        Assert.Equal(2, requests.Sent.Count);
        AssertValidateThenPut(requests.Sent, Deployment);
        foreach (CapturedHttpRequest request in requests.Sent)
        {
            JsonNode properties = RequestBody(request)["properties"]!;
            Assert.Equal(specId, properties["templateLink"]?["id"]?.GetValue<string>());
            Assert.Equal("from-binding", properties["parameters"]?["message"]?["value"]?.GetValue<string>());
            Assert.Equal(3, properties["parameters"]?["count"]?["value"]?.GetValue<int>());
        }
    }

    [Fact]
    public void PowerShellCmdlets_ConflictingParameterSets_AreRejectedBeforeSendingRequests()
    {
        var requests = new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord());
        using var backend = new CmdletBackend(_ => throw new InvalidOperationException("No request expected."));
        using var host = new ScenarioHost(ClientType.PowerShell, requests, backendHandler: backend, extensions: s_whatIfExtensions);

        var error = Record.Exception(() => host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Name"] = Deployment, ["Filter"] = SucceededFilter }));

        Assert.NotNull(error);
        Assert.Contains("parameter set", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(requests.Sent);
    }

    [Fact]
    public void PowerShellCmdlets_BindWhatIfOptions()
    {
        var requests = new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord());
        using var backend = new CmdletBackend(_ => CmdletResponse("""
            {"status":"Succeeded","properties":{"changes":[
              {"resourceId":"/providers/Microsoft.Management/managementGroups/scenario-predicted","changeType":"Create"}
            ]}}
            """));
        using var host = new ScenarioHost(ClientType.PowerShell, requests, backendHandler: backend, extensions: s_whatIfExtensions);
        var parameters = DeployParameters(WhatIfDeployment, SubscriptionId, WhatIfResourceGroup);
        parameters["ResultFormat"] = "ResourceIdOnly";
        parameters["ValidationLevel"] = "ProviderNoRbac";

        AssertWhatIfResult(host.RunPowerShellCmdlet<GetTenantDeploymentWhatIfResultCmdlet>(parameters), "ResourceIdOnly");
        AssertWhatIfRequest(Assert.Single(requests.Sent), "ResourceIdOnly", "ProviderNoRbac");
    }

    private static string CmdletDeploymentBody(string name, string provisioningState = "Succeeded") => JsonSerializer.Serialize(new
    {
        id = $"/providers/Microsoft.Resources/deployments/{name}",
        name,
        location = Location,
        properties = new { provisioningState },
    });

    private static HttpResponseMessage CmdletResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class CmdletBackend(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = respond(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private void RunCliLifecycle(ScenarioHost host, RecordPlaybackInterceptor requests, SessionRecord record, string subscriptionId, string slowTemplateFile)
    {
        bool live = _mode == RecorderMode.Record;

        int sent = requests.Sent.Count;
        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunCli(DeployArguments("validate", Deployment, subscriptionId, ResourceGroup))));
        Assert.Null(validation.Error);
        Assert.Equal("Succeeded", validation.Properties?.ProvisioningState);
        CapturedHttpRequest validate = Since(requests, sent)[0];
        AssertRequest(validate, "POST", $"{DeploymentsUri}/{Deployment}/validate?{ApiVersion}");
        AssertDeploymentBody(validate, subscriptionId, ResourceGroup);

        sent = requests.Sent.Count;
        var created = AssertDeployment(host.RunCli(DeployArguments("create", Deployment, subscriptionId, ResourceGroup)), Deployment, "Succeeded");
        Assert.Equal(DeploymentMode.Incremental, created.Properties?.Mode);
        AssertValidateThenPut(Since(requests, sent), Deployment);

        sent = requests.Sent.Count;
        AssertListed(host.RunCli("deployment", "tenant", "list"), Deployment, live);
        AssertListRequests(Since(requests, sent), $"{DeploymentsUri}?{ApiVersion}");

        sent = requests.Sent.Count;
        AssertListed(host.RunCli("deployment", "tenant", "list", "--filter", SucceededFilter), Deployment, live);
        AssertListRequests(Since(requests, sent), $"{DeploymentsUri}?{ApiVersion}&$filter={Uri.EscapeDataString(SucceededFilter)}");

        AssertDeployment(host.RunCli("deployment", "tenant", "show", "--name", Deployment), Deployment, "Succeeded");

        sent = requests.Sent.Count;
        AssertExportedTemplate(Assert.IsType<JsonElement>(Assert.Single(host.RunCli("deployment", "tenant", "export", "--name", Deployment))));
        AssertRequest(Assert.Single(Since(requests, sent)), "POST", $"{DeploymentsUri}/{Deployment}/exportTemplate?{ApiVersion}");

        sent = requests.Sent.Count;
        Assert.Empty(host.RunCli("deployment", "tenant", "delete", "--name", Deployment));
        AssertRequest(Since(requests, sent)[0], "DELETE", $"{DeploymentsUri}/{Deployment}?{ApiVersion}");

        // --no-wait returns once the PUT is submitted, so the PUT is the step's last request.
        sent = requests.Sent.Count;
        AssertSubmitted(host.RunCli([.. DeployArguments("create", CancelDeployment, subscriptionId, ResourceGroup, slowTemplateFile), "--no-wait"]), CancelDeployment);
        IReadOnlyList<CapturedHttpRequest> submit = Since(requests, sent);
        AssertValidateThenPut(submit, CancelDeployment);
        AssertRequest(submit[^1], "PUT", $"{DeploymentsUri}/{CancelDeployment}?{ApiVersion}");

        // Cancel only once the deployment is Running. ARM answers a cancel sent while the deployment is still Accepted
        // with 204, but can drop it: the deployment then runs to Succeeded and never becomes Canceled. Two earlier
        // record runs failed this way, while live probes showed a cancel sent once Running takes effect in seconds.
        sent = requests.Sent.Count;
        Assert.Empty(host.RunCli("deployment", "tenant", "wait", "--name", CancelDeployment, "--custom", RunningCondition, "--interval", "1", "--timeout", WaitTimeout));
        IReadOnlyList<CapturedHttpRequest> runningPolls = Since(requests, sent);
        Assert.All(runningPolls, poll => AssertRequest(poll, "GET", $"{DeploymentsUri}/{CancelDeployment}?{ApiVersion}"));
        AssertOnlyLastPollRunning(record, runningPolls.Count);

        sent = requests.Sent.Count;
        try
        {
            Assert.Empty(host.RunCli("deployment", "tenant", "cancel", "--name", CancelDeployment));
        }
        catch (HttpRequestException error) when (error.Message.Contains("DeploymentCannotBeCancelled"))
        {
            Assert.Fail("The deployment finished before it could be cancelled; re-record this test.");
        }
        AssertRequest(Assert.Single(Since(requests, sent)), "POST", $"{DeploymentsUri}/{CancelDeployment}/cancel?{ApiVersion}");

        AssertCancelPending(host.RunCli("deployment", "tenant", "show", "--name", CancelDeployment), CancelDeployment);

        sent = requests.Sent.Count;
        Assert.Empty(host.RunCli("deployment", "tenant", "wait", "--name", CancelDeployment, "--custom", CanceledCondition, "--interval", "1", "--timeout", WaitTimeout));
        IReadOnlyList<CapturedHttpRequest> polls = Since(requests, sent);
        Assert.All(polls, poll => AssertRequest(poll, "GET", $"{DeploymentsUri}/{CancelDeployment}?{ApiVersion}"));
        AssertOnlyLastPollCanceled(record, polls.Count);

        AssertDeployment(host.RunCli("deployment", "tenant", "show", "--name", CancelDeployment), CancelDeployment, "Canceled");
    }

    // Playback only: the same requests as the CLI workflow, through the PowerShell command names.
    private void RunPowerShellLifecycle(ScenarioHost host, string slowTemplateFile)
    {
        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(
            host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(DeployParameters(Deployment, SubscriptionId, ResourceGroup))));
        Assert.Null(validation.Error);
        Assert.Equal("Succeeded", validation.Properties?.ProvisioningState);

        var created = AssertDeployment(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(DeployParameters(Deployment, SubscriptionId, ResourceGroup)), Deployment, "Succeeded");
        Assert.Equal(DeploymentMode.Incremental, created.Properties?.Mode);

        AssertDeployment(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(new Dictionary<string, object>()), Deployment, "Succeeded");
        AssertDeployment(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(new Dictionary<string, object> { ["Filter"] = SucceededFilter }), Deployment, "Succeeded");
        AssertDeployment(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(Named(Deployment)), Deployment, "Succeeded");

        // An existing folder receives "<deployment>.json".
        string folder = Path.Combine(Path.GetTempPath(), $"azclips-tenant-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(host.RunPowerShellCmdlet<SaveTenantDeploymentTemplateCmdlet>(new Dictionary<string, object>
            {
                ["DeploymentName"] = Deployment,
                ["Path"] = folder,
                ["Force"] = true,
            })));
            Assert.Equal(Path.Combine(folder, $"{Deployment}.json"), saved.Path);
            string content = File.ReadAllText(saved.Path);
            Assert.Contains("\n", content);
            using var document = JsonDocument.Parse(content);
            AssertExportedTemplate(document.RootElement);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        Assert.Empty(host.RunPowerShellCmdlet<RemoveTenantDeploymentCmdlet>(Named(Deployment)));

        Dictionary<string, object> submit = DeployParameters(CancelDeployment, SubscriptionId, ResourceGroup, slowTemplateFile);
        submit["NoWait"] = true;
        AssertSubmitted(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(submit), CancelDeployment);

        // Like the CLI workflow, cancel only once Running: a cancel sent while still Accepted can be dropped by ARM.
        Assert.Equal("Running", PollUntil(host, "Running"));

        Assert.Empty(host.RunPowerShellCmdlet<StopTenantDeploymentCmdlet>(Named(CancelDeployment)));

        AssertCancelPending(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(Named(CancelDeployment)), CancelDeployment);

        Assert.Equal("Canceled", PollUntil(host, "Canceled"));

        AssertDeployment(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(Named(CancelDeployment)), CancelDeployment, "Canceled");
    }

    // PowerShell has no wait command, so it polls show; playback serves the GETs the matching CLI wait recorded.
    private string? PollUntil(ScenarioHost host, string expectedState)
    {
        string? state = null;
        for (int attempt = 0; attempt < MaxWaitPolls && state != expectedState; attempt++)
        {
            state = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(Named(CancelDeployment)))).Properties?.ProvisioningState;
        }
        return state;
    }

    private void RunCliWhatIf(
        ScenarioHost host, RecordPlaybackInterceptor requests, string subscriptionId, string? resultFormat, string? validationLevel)
    {
        var arguments = new List<string>(DeployArguments("what-if", WhatIfDeployment, subscriptionId, WhatIfResourceGroup));
        if (resultFormat is not null)
        {
            arguments.AddRange(["--result-format", resultFormat]);
        }
        if (validationLevel is not null)
        {
            arguments.AddRange(["--validation-level", validationLevel]);
        }

        int sent = requests.Sent.Count;
        AssertWhatIfResult(host.RunCli([.. arguments]), resultFormat);
        AssertWhatIfRequest(Since(requests, sent)[0], resultFormat, validationLevel);
    }

    private void AssertWhatIfRequest(CapturedHttpRequest whatIf, string? resultFormat, string? validationLevel)
    {
        AssertRequest(whatIf, "POST", $"{DeploymentsUri}/{WhatIfDeployment}/whatIf?{ApiVersion}");
        JsonNode properties = RequestBody(whatIf)["properties"]!;
        Assert.Equal(resultFormat ?? "FullResourcePayloads", properties["whatIfSettings"]?["resultFormat"]?.GetValue<string>());
        Assert.Equal("Incremental", properties["mode"]?.GetValue<string>());
        Assert.Equal(validationLevel, properties["validationLevel"]?.GetValue<string>());
    }

    private string[] DeployArguments(string command, string deployment, string subscriptionId, string resourceGroup, string? templateFile = null) =>
    [
        "deployment", "tenant", command, "--name", deployment, "--location", Location, "--template-file", templateFile ?? _templateFile,
        "--parameter", $"nestedSubId={subscriptionId}", "--parameter", $"nestedRG={resourceGroup}",
    ];

    private Dictionary<string, object> DeployParameters(string deployment, string subscriptionId, string resourceGroup, string? templateFile = null) => new()
    {
        ["Name"] = deployment,
        ["Location"] = Location,
        ["TemplateFile"] = templateFile ?? _templateFile,
        ["Parameter"] = new[] { $"nestedSubId={subscriptionId}", $"nestedRG={resourceGroup}" },
    };

    private static Dictionary<string, object> Named(string deployment) => new() { ["Name"] = deployment };

    private static void TryCleanUp(List<Exception> failures, string kind, string name, Action delete)
    {
        try
        {
            delete();
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Cleanup of {kind} '{name}' failed: {error}");
            failures.Add(new InvalidOperationException($"Cleanup of {kind} '{name}' failed.", error));
        }
    }

    private static void AssertWhatIfResult(IReadOnlyList<object> output, string? resultFormat = null)
    {
        var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output));
        Assert.Equal("Succeeded", result.Status);
        Assert.Null(result.Error);
        // The nested resource group never exists, so every predicted change is a Create.
        Assert.NotEmpty(result.Properties?.Changes ?? []);
        Assert.All(result.Properties!.Changes!, change => Assert.Equal("Create", change.ChangeType));
        if (resultFormat == "ResourceIdOnly")
        {
            Assert.All(result.Properties.Changes!, change =>
            {
                Assert.False(string.IsNullOrWhiteSpace(change.ResourceId));
                Assert.Null(change.Before);
                Assert.Null(change.After);
                Assert.Null(change.Delta);
            });
        }
    }

    private static DeploymentExtended AssertDeployment(IReadOnlyList<object> output, string name, string provisioningState)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(output));
        Assert.Equal(name, deployment.Name);
        Assert.Equal(provisioningState, deployment.Properties?.ProvisioningState);
        return deployment;
    }

    private static void AssertSubmitted(IReadOnlyList<object> output, string name)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(output));
        Assert.Equal(name, deployment.Name);
        string? state = deployment.Properties?.ProvisioningState;
        Assert.True(state is "Accepted" or "Running", $"The submitted deployment is already '{state}', so it cannot be cancelled; re-record this test.");
    }

    // ARM can accept the cancel and still let the deployment finish, so fail fast instead of waiting out the timeout.
    private static void AssertCancelPending(IReadOnlyList<object> output, string name)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(output));
        Assert.Equal(name, deployment.Name);
        string? state = deployment.Properties?.ProvisioningState;
        Assert.False(state is "Succeeded" or "Failed", $"The deployment ended '{state}' despite the cancel; re-record this test.");
    }

    // Record mode sees the whole tenant, so it only checks the deployment is listed once. Playback sees the recording,
    // which the list sanitizer trimmed to this scenario's deployments.
    private static void AssertListed(IReadOnlyList<object> output, string name, bool live)
    {
        var deployments = Assert.IsType<List<DeploymentExtended>>(Assert.Single(output));
        if (live)
        {
            Assert.Single(deployments, deployment => string.Equals(deployment.Name, name, StringComparison.OrdinalIgnoreCase));
            return;
        }

        Assert.Equal(name, Assert.Single(deployments).Name);
    }

    // The first request carries the query; any later GET follows a nextLink page.
    private static void AssertListRequests(IReadOnlyList<CapturedHttpRequest> requests, string firstUri)
    {
        AssertRequest(requests[0], "GET", firstUri);
        Assert.DoesNotContain("$top", requests[0].Uri, StringComparison.Ordinal);
        Assert.All(requests, request => Assert.Equal("GET", request.Method));
    }

    private static void AssertValidateThenPut(IReadOnlyList<CapturedHttpRequest> requests, string name)
    {
        string validateUri = new Uri($"{DeploymentsUri}/{name}/validate?{ApiVersion}").AbsoluteUri;
        string putUri = new Uri($"{DeploymentsUri}/{name}?{ApiVersion}").AbsoluteUri;
        int validate = IndexOf(requests, "POST", validateUri);
        int put = IndexOf(requests, "PUT", putUri);
        Assert.True(validate >= 0 && put > validate, $"Expected POST {validateUri} before PUT {putUri}.");
        Assert.Equal(requests[validate].Body, requests[put].Body);
    }

    private static int IndexOf(IReadOnlyList<CapturedHttpRequest> requests, string method, string uri)
    {
        for (int index = 0; index < requests.Count; index++)
        {
            if (requests[index].Method == method && string.Equals(requests[index].Uri, uri, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static void AssertDeploymentBody(CapturedHttpRequest request, string subscriptionId, string resourceGroup)
    {
        JsonNode body = RequestBody(request);
        Assert.Equal(Location, body["location"]?.GetValue<string>());

        JsonNode properties = body["properties"]!;
        Assert.Equal("Incremental", properties["mode"]?.GetValue<string>());
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(Template, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }), properties["template"]),
            $"The request template differs from the template file: {properties["template"]}");
        Assert.Null(properties["templateLink"]);
        Assert.True(
            JsonNode.DeepEquals(
                new JsonObject
                {
                    ["nestedSubId"] = new JsonObject { ["value"] = subscriptionId },
                    ["nestedRG"] = new JsonObject { ["value"] = resourceGroup },
                },
                properties["parameters"]),
            $"Unexpected request parameters: {properties["parameters"]}");
        Assert.Null(properties["parametersLink"]);
    }

    private static void AssertExportedTemplate(JsonElement template)
    {
        Assert.True(template.TryGetProperty("$schema", out _), $"Exported template has no $schema: {template}");
        Assert.False(template.TryGetProperty("template", out _), $"Exported template still has the template wrapper: {template}");
        // The nested deployment is the template's only resource.
        Assert.Equal(1, template.GetProperty("resources").GetArrayLength());
    }

    // The wait responses are read from the session record: record mode stores entries in send order and playback
    // consumes them in that order. Recorded URIs carry the base name.

    // The wait for Running directly follows the second deployment's no-wait PUT.
    private static void AssertOnlyLastPollRunning(SessionRecord record, int pollCount)
    {
        int put = RecordedEntryIndex(record, "PUT", CancelDeploymentRecordedUri);
        AssertOnlyLastPoll(record, put + 1, pollCount, "Running");
    }

    // The wait for Canceled follows the cancel and the show right after it.
    private static void AssertOnlyLastPollCanceled(SessionRecord record, int pollCount)
    {
        int cancel = RecordedEntryIndex(record, "POST", $"{DeploymentsUri}/{CancelDeploymentBaseName}/cancel?{ApiVersion}");

        SessionRecordEntry? show = record.Entries.ElementAtOrDefault(cancel + 1);
        Assert.True(
            show?.Request.Method == "GET" && string.Equals(show.Request.Uri, CancelDeploymentRecordedUri, StringComparison.OrdinalIgnoreCase),
            $"The recorded entry after the cancel is not GET {CancelDeploymentRecordedUri}.");

        AssertOnlyLastPoll(record, cancel + 2, pollCount, "Canceled");
    }

    private static int RecordedEntryIndex(SessionRecord record, string method, string uri)
    {
        int index = record.Entries.FindIndex(entry =>
            entry.Request.Method == method && string.Equals(entry.Request.Uri, uri, StringComparison.OrdinalIgnoreCase));
        Assert.True(index >= 0, $"No recorded {method} {uri}.");
        return index;
    }

    private static void AssertOnlyLastPoll(SessionRecord record, int firstPoll, int pollCount, string expectedState)
    {
        var polls = record.Entries.Skip(firstPoll).Take(pollCount).ToList();
        Assert.Equal(pollCount, polls.Count);
        for (int index = 0; index < polls.Count; index++)
        {
            Assert.Equal("GET", polls[index].Request.Method);
            Assert.Equal(CancelDeploymentRecordedUri, polls[index].Request.Uri, ignoreCase: true);
            string? state = ProvisioningState(polls[index].Response.Body);
            if (index == polls.Count - 1)
            {
                Assert.Equal(expectedState, state);
            }
            else
            {
                Assert.NotEqual(expectedState, state);
            }
        }
    }

    private static string? ProvisioningState(string? body) =>
        string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body)?["properties"]?["provisioningState"]?.GetValue<string>();

    private static void AssertRequest(CapturedHttpRequest request, string method, string uri)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal(new Uri(uri).AbsoluteUri, request.Uri, ignoreCase: true);
    }

    // The requests a step sent: everything after the count taken before it ran.
    private static IReadOnlyList<CapturedHttpRequest> Since(RecordPlaybackInterceptor requests, int start)
    {
        IReadOnlyList<CapturedHttpRequest> step = [.. requests.Sent.Skip(start)];
        Assert.NotEmpty(step);
        return step;
    }

    private static JsonNode RequestBody(CapturedHttpRequest request) =>
        JsonNode.Parse(request.Body ?? throw new InvalidOperationException($"{request.Method} {request.Uri} has no body."))!;

    // Scenario sanitisers run before the default set, so the list trim still sees this run's scoped names.
    private static RecordingSanitizers Sanitizers(params IRecordingSanitizer[] scenarioSanitizers) =>
        new([.. scenarioSanitizers, .. RecordingSanitizers.Default.Sanitizers], [.. RecordingSanitizers.Default.RemovedHeaders]);

    private sealed class TemplateFile : IDisposable
    {
        public TemplateFile(string content) => File.WriteAllText(Path, content);

        public string Path { get; } = System.IO.Path.GetTempFileName();

        public void Dispose() => File.Delete(Path);
    }

    /// <summary>
    /// Rewrites the live subscription id wherever it appears. The default rule only rewrites a GUID after
    /// <c>/subscriptions/</c>, but the template parameter <c>nestedSubId</c> carries it bare in request and response
    /// bodies. Set <see cref="SubscriptionId"/> before the first request is recorded; playback leaves it unset.
    /// </summary>
    private sealed class LiveSubscriptionSanitizer : IRecordingSanitizer
    {
        public string? SubscriptionId { get; set; }

        public string Name => "live-subscription-id";

        public string Sanitize(string value) =>
            string.IsNullOrEmpty(SubscriptionId)
                ? value
                : Regex.Replace(value, Regex.Escape(SubscriptionId), RecordingSanitizers.PlaceholderGuid, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Keeps only this scenario's deployments in a tenant deployment list response, which otherwise holds every
    /// tenant-root deployment the recorder can see.
    /// </summary>
    private sealed class TenantDeploymentListSanitizer(params string[] deploymentNames) : IRecordingSanitizer
    {
        public string Name => "tenant-deployment-list";

        public string Sanitize(string value)
        {
            if (!value.Contains("\"value\"", StringComparison.Ordinal))
            {
                return value;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(value) as JsonObject;
            }
            catch (JsonException)
            {
                return value;
            }

            if (root?["value"] is not JsonArray items)
            {
                return value;
            }

            var others = items
                .Where(item => item?["id"] is JsonValue id
                               && id.TryGetValue<string>(out var resourceId)
                               && !deploymentNames.Any(name => resourceId.EndsWith($"/providers/Microsoft.Resources/deployments/{name}", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (others.Count == 0)
            {
                return value;
            }

            foreach (var item in others)
            {
                items.Remove(item);
            }
            return root.ToJsonString();
        }
    }
}
