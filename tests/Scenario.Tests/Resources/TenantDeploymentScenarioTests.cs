// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Tenant-scope deployment scenarios, one per upstream azure-cli tenant test. Each test has its own recording,
/// template file, run-scoped names and sanitizers. Record mode runs the CLI surface against live Azure; playback
/// replays the recording on both surfaces with body matching, so PowerShell must send the same requests.
/// <para>
/// Recording needs:
/// <list type="bullet">
/// <item><see cref="TenantDeploymentLifecycle"/> (T1): <c>Microsoft.Resources/deployments/write</c> at the tenant root
/// (<c>/</c>) and resource-group create rights in the signed-in subscription.</item>
/// <item><see cref="TenantDeploymentWhatIf"/> (T2): permission to run what-if at the tenant root (<c>/</c>).</item>
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
    private const string CanceledCondition = "properties.provisioningState=='Canceled'";

    // Remove once the recordings exist: no recording could be made yet, because recording needs deployment rights at
    // the tenant root (/), and playback without a recording always fails.
    private const string RecordingRequired =
        "Recording required: record with an account that has Microsoft.Resources/deployments rights at the tenant root (/); see the class summary.";

    private const string DeploymentBaseName = "scenario-tenant-deployment";
    private const string CancelDeploymentBaseName = "scenario-tenant-deployment-cancel";
    private const string ResourceGroupBaseName = "scenario-tenant-deployment-rg";
    private const string WhatIfDeploymentBaseName = "scenario-tenant-what-if";
    private const string WhatIfResourceGroupBaseName = "scenario-tenant-what-if-rg";

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
          // The deployment only creates a resource group in the subscription.
        }
        """;

    private static readonly ExtensionUnderTest[] s_lifecycleExtensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments];

    private static readonly ExtensionUnderTest[] s_whatIfExtensions = [ExtensionUnderTest.ResourcesDeployments];

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

    // T1: verifies "T1 Tenant deployment lifecycle: validate, create, list, show, export, delete, then cancel and wait on
    // a second deployment" of resources-deployments-tests (requirement "Create validates, then deploys and waits").
    // Upstream: azure-cli test_resource.py DeploymentTestAtTenantScope.test_tenant_level_deployment.
    [Fact(Skip = RecordingRequired)]
    public void TenantDeploymentLifecycle()
    {
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
                RunCliLifecycle(host, requests, recording.Record, liveSubscriptionId);
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
            RunCliLifecycle(cli, cliRequests, recording.Record, SubscriptionId);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_lifecycleExtensions))
        {
            RunPowerShellLifecycle(ps);
        }
    }

    // T2: verifies "T2 Tenant deployment what-if with the default result format" of resources-deployments-tests
    // (requirement "What-if a deployment").
    // Upstream: azure-cli test_resource.py DeploymentWhatIfAtTenantScopeTest.test_tenant_level_what_if.
    [Fact(Skip = RecordingRequired)]
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
            RunCliWhatIf(host, requests, liveSubscriptionId);
            recording.Save();
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliRequests, extensions: s_whatIfExtensions))
        {
            RunCliWhatIf(cli, cliRequests, SubscriptionId);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_whatIfExtensions))
        {
            AssertWhatIfResult(ps.RunPowerShell("Get-AzTenantDeploymentWhatIfResult", DeployParameters(WhatIfDeployment, SubscriptionId, WhatIfResourceGroup)));
        }
    }

    private void RunCliLifecycle(ScenarioHost host, RecordPlaybackInterceptor requests, SessionRecord record, string subscriptionId)
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
        AssertSubmitted(host.RunCli([.. DeployArguments("create", CancelDeployment, subscriptionId, ResourceGroup), "--no-wait"]), CancelDeployment);
        IReadOnlyList<CapturedHttpRequest> submit = Since(requests, sent);
        AssertValidateThenPut(submit, CancelDeployment);
        AssertRequest(submit[^1], "PUT", $"{DeploymentsUri}/{CancelDeployment}?{ApiVersion}");

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

        sent = requests.Sent.Count;
        Assert.Empty(host.RunCli("deployment", "tenant", "wait", "--name", CancelDeployment, "--custom", CanceledCondition, "--interval", "1"));
        IReadOnlyList<CapturedHttpRequest> polls = Since(requests, sent);
        Assert.All(polls, poll => AssertRequest(poll, "GET", $"{DeploymentsUri}/{CancelDeployment}?{ApiVersion}"));
        AssertOnlyLastPollCanceled(record, polls.Count);

        AssertDeployment(host.RunCli("deployment", "tenant", "show", "--name", CancelDeployment), CancelDeployment, "Canceled");
    }

    // Playback only: the same requests as the CLI workflow, through the PowerShell command names.
    private void RunPowerShellLifecycle(ScenarioHost host)
    {
        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(
            host.RunPowerShell("Test-AzTenantDeployment", DeployParameters(Deployment, SubscriptionId, ResourceGroup))));
        Assert.Null(validation.Error);
        Assert.Equal("Succeeded", validation.Properties?.ProvisioningState);

        var created = AssertDeployment(host.RunPowerShell("New-AzTenantDeployment", DeployParameters(Deployment, SubscriptionId, ResourceGroup)), Deployment, "Succeeded");
        Assert.Equal(DeploymentMode.Incremental, created.Properties?.Mode);

        AssertListed(host.RunPowerShell("Get-AzTenantDeployment_List", new Dictionary<string, object>()), Deployment, live: false);
        AssertListed(host.RunPowerShell("Get-AzTenantDeployment_List", new Dictionary<string, object> { ["Filter"] = SucceededFilter }), Deployment, live: false);
        AssertDeployment(host.RunPowerShell("Get-AzTenantDeployment_Get", Named(Deployment)), Deployment, "Succeeded");

        // An existing folder receives "<deployment>.json".
        string folder = Path.Combine(Path.GetTempPath(), $"azclips-tenant-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(host.RunPowerShell("Save-AzTenantDeploymentTemplate", new Dictionary<string, object>
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

        Assert.Empty(host.RunPowerShell("Remove-AzTenantDeployment", Named(Deployment)));

        Dictionary<string, object> submit = DeployParameters(CancelDeployment, SubscriptionId, ResourceGroup);
        submit["NoWait"] = true;
        AssertSubmitted(host.RunPowerShell("New-AzTenantDeployment", submit), CancelDeployment);

        Assert.Empty(host.RunPowerShell("Stop-AzTenantDeployment", Named(CancelDeployment)));

        // PowerShell has no wait command, so it polls show; playback serves the GETs the CLI wait recorded.
        string? state = null;
        for (int attempt = 0; attempt < 30 && state != "Canceled"; attempt++)
        {
            state = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunPowerShell("Get-AzTenantDeployment_Get", Named(CancelDeployment)))).Properties?.ProvisioningState;
        }
        Assert.Equal("Canceled", state);

        AssertDeployment(host.RunPowerShell("Get-AzTenantDeployment_Get", Named(CancelDeployment)), CancelDeployment, "Canceled");
    }

    private void RunCliWhatIf(ScenarioHost host, RecordPlaybackInterceptor requests, string subscriptionId)
    {
        int sent = requests.Sent.Count;
        AssertWhatIfResult(host.RunCli(DeployArguments("what-if", WhatIfDeployment, subscriptionId, WhatIfResourceGroup)));

        CapturedHttpRequest whatIf = Since(requests, sent)[0];
        AssertRequest(whatIf, "POST", $"{DeploymentsUri}/{WhatIfDeployment}/whatIf?{ApiVersion}");
        JsonNode properties = RequestBody(whatIf)["properties"]!;
        Assert.Equal("FullResourcePayloads", properties["whatIfSettings"]?["resultFormat"]?.GetValue<string>());
        Assert.Equal("Incremental", properties["mode"]?.GetValue<string>());
    }

    private string[] DeployArguments(string command, string deployment, string subscriptionId, string resourceGroup) =>
    [
        "deployment", "tenant", command, "--name", deployment, "--location", Location, "--template-file", _templateFile,
        "--parameter", $"nestedSubId={subscriptionId}", "--parameter", $"nestedRG={resourceGroup}",
    ];

    private Dictionary<string, object> DeployParameters(string deployment, string subscriptionId, string resourceGroup) => new()
    {
        ["Name"] = deployment,
        ["Location"] = Location,
        ["TemplateFile"] = _templateFile,
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

    private static void AssertWhatIfResult(IReadOnlyList<object> output)
    {
        var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output));
        Assert.Equal("Succeeded", result.Status);
        Assert.Null(result.Error);
        // The nested resource group never exists, so every predicted change is a Create.
        Assert.NotEmpty(result.Properties?.Changes ?? []);
        Assert.All(result.Properties!.Changes!, change => Assert.Equal("Create", change.ChangeType));
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

    // The wait responses are the recorded entries right after the cancel: record mode stores them in send order and
    // playback consumes them in that order. Recorded URIs carry the base name.
    private static void AssertOnlyLastPollCanceled(SessionRecord record, int pollCount)
    {
        string cancelUri = $"{DeploymentsUri}/{CancelDeploymentBaseName}/cancel?{ApiVersion}";
        int cancel = record.Entries.FindIndex(entry =>
            entry.Request.Method == "POST" && string.Equals(entry.Request.Uri, cancelUri, StringComparison.OrdinalIgnoreCase));
        Assert.True(cancel >= 0, $"No recorded POST {cancelUri}.");

        var polls = record.Entries.Skip(cancel + 1).Take(pollCount).ToList();
        Assert.Equal(pollCount, polls.Count);
        for (int index = 0; index < polls.Count; index++)
        {
            Assert.Equal("GET", polls[index].Request.Method);
            string? state = ProvisioningState(polls[index].Response.Body);
            if (index == polls.Count - 1)
            {
                Assert.Equal("Canceled", state);
            }
            else
            {
                Assert.NotEqual("Canceled", state);
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
