using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Runs <c>deployment sub</c> through the real dispatcher, binders, and ARM pipeline in one
/// recorded session. The steps are validate, what-if, create, wait, show, list, export, the cancel
/// rejections, delete, cancelling an in-flight deployment, and the error paths: invalid arguments,
/// wait timeouts, a deployment that fails, and a what-if whose polled result fails. Record mode captures the CLI
/// workflow against Azure. Playback replays the same traffic through the CLI and PowerShell
/// surfaces and matches request bodies, so both surfaces must bind their arguments into the same
/// recorded requests. PowerShell has no what-if, wait, or export cmdlet at subscription scope, so
/// those steps run on the CLI only.
/// </summary>
[Collection("Scenario Tests")]
public sealed class SubscriptionDeploymentScenarioTests
{
    // A method can't share its class's name, so the recording is named explicitly.
    private const string RecordingName = nameof(SubscriptionDeploymentScenarioTests);
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string DeploymentName = "scenario-sub-deployment";
    private const string MissingDeploymentName = "scenario-sub-missing-deployment";
    private const string SlowDeploymentName = "scenario-sub-slow-deployment";
    private const string SlowResourceGroupName = "scenario-sub-slow-rg";
    private const string FailedDeploymentName = "scenario-sub-failed-deployment";
    private const string InvalidWhatIfName = "scenario-sub-invalid-what-if";
    private const string Location = "westus2";
    private const string Greeting = "hello";
    private const string Template =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"greeting":{"type":"string"}},"resources":[],"outputs":{"greeting":{"type":"string","value":"[parameters('greeting')]"}}}""";
    // Passes the client-side checks, but ARM rejects the undeclared parameter while it runs the
    // what-if, so the POST returns 202 and the polled result comes back Failed.
    private const string InvalidTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('undeclared')]","location":"westus2"}]}""";

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments, ExtensionUnderTest.Rest];

    [Fact]
    public void ValidateWhatIfCreateShowListExportCancelDeleteSubscriptionDeployment()
    {
        var mode = RecorderModeResolver.FromEnvironment();
        // Runs before the run-scoped-name sanitizer, so only this run's deployments survive.
        var listTrim = new DeploymentListSanitizer(
            [TestRun.ScopedName(DeploymentName, mode), TestRun.ScopedName(SlowDeploymentName, mode)]);
        var sanitizers = new RecordingSanitizers(
            [listTrim, .. RecordingSanitizers.Default.Sanitizers],
            [.. RecordingSanitizers.Default.RemovedHeaders]);
        var recording = SessionRecordFiles.OpenSession(
            typeof(SubscriptionDeploymentScenarioTests), RecordingName, new DefaultRequestMatcher(matchBody: true), sanitizers);
        string template = Path.GetTempFileName();
        File.WriteAllText(template, Template);
        string invalidTemplate = Path.GetTempFileName();
        File.WriteAllText(invalidTemplate, InvalidTemplate);

        try
        {
            if (recording.Mode == RecorderMode.Record)
            {
                using var host = new ScenarioHost(
                    ClientType.CLI,
                    recording.CreateInterceptor(),
                    useLiveAuth: true,
                    extensions: s_extensions);
                var target = new Target(host, ClientType.CLI, host.LiveSubscriptionId(), Live: true);

                try
                {
                    Lifecycle(target, template, invalidTemplate);
                    AssertRequestBodies(recording.Record);
                    recording.Save();
                }
                finally
                {
                    // Runs after the save, so the recording never includes the cleanup.
                    foreach (var name in new[] { DeploymentName, SlowDeploymentName, FailedDeploymentName })
                    {
                        try { Delete(target, target.Name(name)); } catch { }
                    }
                    try { host.RunCli("group", "delete", "--subscription-id", target.SubscriptionId, "--name", target.Name(SlowResourceGroupName)); } catch { }
                }
                return;
            }

            // Body matching already proves each surface sent exactly the recorded requests. These
            // assertions check the values inside those requests.
            AssertRequestBodies(recording.Record);

            foreach (var surface in new[] { ClientType.CLI, ClientType.PowerShell })
            {
                using var host = new ScenarioHost(surface, recording.CreateInterceptor(), extensions: s_extensions);
                Lifecycle(new Target(host, surface, SubscriptionId, Live: false), template, invalidTemplate);
            }
        }
        finally
        {
            File.Delete(template);
            File.Delete(invalidTemplate);
        }
    }

    // One surface; Run picks the CLI or PowerShell form of a command. Name scopes a base name to
    // the record run, and the run-scoped-name sanitizer strips the suffix from the recording.
    private sealed record Target(ScenarioHost Host, ClientType Surface, string SubscriptionId, bool Live)
    {
        public string Name(string baseName) => Live ? TestRun.ScopedName(baseName, RecorderMode.Record) : baseName;

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

    private static void Lifecycle(Target target, string template, string invalidTemplate)
    {
        string name = target.Name(DeploymentName);

        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(WithTemplate(target, "validate", "Test-AzDeployment", name, template)));
        Assert.Null(validation.Error);
        Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);

        if (target.Surface == ClientType.CLI)
        {
            var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Host.RunCli(WithTemplateCli(target, "what-if", name, template))));
            Assert.Equal(State.Succeeded, whatIf.Status);
            Assert.Null(whatIf.Error);
            Assert.Empty(whatIf.Properties?.Changes ?? []);
        }

        AssertSucceeded(target, WithTemplate(target, "create", "New-AzDeployment", name, template));

        if (target.Surface == ClientType.CLI)
        {
            Assert.Empty(target.Host.RunCli(Cli(target, "wait", name, "--created", "--interval", "1")));
        }

        AssertSucceeded(target, Show(target, name));

        Assert.Contains(name, ListNames(target));

        if (target.Surface == ClientType.CLI)
        {
            var exported = Assert.IsType<JsonElement>(Assert.Single(target.Host.RunCli(Cli(target, "export", name))));
            Assert.Contains("subscriptionDeploymentTemplate", exported.GetProperty("$schema").GetString());
            Assert.True(exported.GetProperty("outputs").TryGetProperty("greeting", out _), $"Exported template has no greeting output: {exported}");
        }

        AssertCancelFails("DeploymentCannotBeCancelled", target, name);
        AssertCancelFails("DeploymentNotFound", target, target.Name(MissingDeploymentName));

        Assert.Empty(Delete(target, name));

        if (target.Surface == ClientType.CLI)
        {
            Assert.Empty(target.Host.RunCli(Cli(target, "wait", name, "--deleted", "--interval", "1")));
        }

        Assert.DoesNotContain(name, ListNames(target));

        CancelInFlightDeployment(target);

        Assert.Empty(Delete(target, target.Name(SlowDeploymentName)));

        AssertInvalidArgumentsFail(target, template);

        if (target.Surface == ClientType.CLI)
        {
            AssertWaitTimesOut(target);
            AssertWaitOnFailedDeploymentFails(target);
            AssertWhatIfPolledFailure(target, invalidTemplate);
        }
    }

    // These throw before any request is sent. Playback would fail on an unrecorded request, so a
    // passing run also proves nothing reached ARM.
    private static void AssertInvalidArgumentsFail(Target target, string template)
    {
        string name = target.Name(MissingDeploymentName);

        // CLI fails in argument binding and PowerShell in the processor; both are client-side errors.
        var noLocation = Assert.ThrowsAny<AzclipsErrorException>(() => target.Run(
            Cli(target, "create", name, "--template-file", template),
            "New-AzDeployment",
            new(PowerShell(target, name)) { ["TemplateFile"] = template }));
        Assert.Contains("location", noLocation.Message, StringComparison.OrdinalIgnoreCase);

        var noTemplate = Assert.ThrowsAny<ArgumentException>(() => target.Run(
            Cli(target, "create", name, "--location", Location),
            "New-AzDeployment",
            new(PowerShell(target, name)) { ["Location"] = Location }));
        Assert.Contains("Exactly one of TemplateUri", noTemplate.Message);

        if (target.Surface == ClientType.PowerShell)
        {
            // Only PowerShell can show a deployment by id.
            var wrongScope = Assert.Throws<InvalidInputException>(() => target.Host.RunPowerShell(
                "Get-AzDeployment_GetById",
                new Dictionary<string, object> { ["Id"] = $"/subscriptions/{target.SubscriptionId}/resourceGroups/group/providers/Microsoft.Resources/deployments/{name}" }));
            Assert.Contains("Invalid deployment id", wrongScope.Message);
            return;
        }

        foreach (var conditions in new[] { Array.Empty<string>(), ["--created", "--deleted"] })
        {
            var error = Assert.Throws<InvalidInputException>(() => target.Host.RunCli(Cli(target, "wait", name, conditions)));
            Assert.Contains("Specify exactly one of --created, --updated, --deleted, or --exists.", error.Message);
        }

        Assert.Throws<TimeoutException>(() => target.Host.RunCli(Cli(target, "wait", name, "--exists", "--timeout", "0")));
    }

    // The recording has one 404 for the missing deployment; the wait stops when the timeout runs
    // out instead of sleeping the full 30-second interval.
    private static void AssertWaitTimesOut(Target target)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<TimeoutException>(() => target.Host.RunCli(
            Cli(target, "wait", target.Name(MissingDeploymentName), "--created", "--timeout", "1", "--interval", "30")));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Wait took {stopwatch.Elapsed}.");
    }

    // The output references a resource group that doesn't exist. ARM accepts the deployment and
    // only fails it when it evaluates the output, so the wait sees it reach Failed.
    private static void AssertWaitOnFailedDeploymentFails(Target target)
    {
        string name = target.Name(FailedDeploymentName);
        string uri = $"https://management.azure.com/subscriptions/{target.SubscriptionId}/providers/Microsoft.Resources/deployments/{name}?api-version=2025-04-01";
        string body = """{"location":"__LOCATION__","properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[],"outputs":{"missing":{"type":"string","value":"[reference(subscriptionResourceId('Microsoft.Resources/resourceGroups', 'scenario-sub-missing-rg'), '2022-09-01').location]"}}}}}"""
            .Replace("__LOCATION__", Location);
        var response = Assert.IsType<RestResponse>(Assert.Single(target.Host.RunCli("rest", "--method", "PUT", "--uri", uri, "--body", body)));
        Assert.Equal(201, response.StatusCode);

        var error = Assert.Throws<InvalidOperationException>(() => target.Host.RunCli(Cli(target, "wait", name, "--created", "--interval", "1")));
        Assert.Equal($"Deployment '{name}' failed.", error.Message);

        Assert.Empty(Delete(target, name));
    }

    private static void AssertWhatIfPolledFailure(Target target, string invalidTemplate)
    {
        var error = Assert.Throws<InvalidOperationException>(() => target.Host.RunCli(
            Cli(target, "what-if", target.Name(InvalidWhatIfName), "--location", Location, "--template-file", invalidTemplate)));
        Assert.Contains("What-if operation failed.", error.Message);
        Assert.Contains("InvalidTemplate", error.Message);
        Assert.Contains("undeclared", error.Message);
    }

    private static void AssertSucceeded(Target target, IReadOnlyList<object> outputs)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(outputs));
        Assert.Equal(target.Name(DeploymentName), deployment.Name);
        Assert.Equal(State.Succeeded, deployment.ProvisioningState);
        Assert.Equal(Greeting, deployment.Properties?.Outputs?["greeting"].Value);
    }

    // The list command writes every page as one collected List<DeploymentExtended>.
    private static List<string?> ListNames(Target target) =>
        [.. Assert.IsType<List<DeploymentExtended>>(Assert.Single(target.Run(
            ["deployment", "sub", "list", "--subscription-id", target.SubscriptionId],
            "Get-AzDeployment_List",
            new() { ["SubscriptionId"] = target.SubscriptionId })))
            .Select(d => d.Name)];

    private static void CancelInFlightDeployment(Target target)
    {
        // `deployment sub create` blocks until the deployment is terminal, so start the
        // deployment to cancel with a raw PUT that returns as soon as ARM accepts it.
        var response = Assert.IsType<RestResponse>(Assert.Single(StartSlowDeployment(target)));
        Assert.Equal(201, response.StatusCode);

        string? state = WaitForState(target, attempts: 15, State.Accepted, State.Running);
        Assert.True(state is State.Accepted or State.Running, $"The deployment to cancel is '{state}', not cancellable; re-record this test.");

        try
        {
            Assert.Empty(Cancel(target, target.Name(SlowDeploymentName)));
        }
        catch (HttpRequestException error) when (error.Message.Contains("DeploymentCannotBeCancelled"))
        {
            Assert.Fail("The deployment finished before it could be cancelled; re-record this test.");
        }

        Assert.Equal(State.Canceled, WaitForState(target, attempts: 30, State.Canceled));
    }

    private static void AssertCancelFails(string expectedCode, Target target, string deploymentName)
    {
        var error = Assert.Throws<HttpRequestException>(() => Cancel(target, deploymentName));
        Assert.Contains(expectedCode, error.Message);
    }

    // Stops at a wanted state or any terminal one. Playback replays the recorded responses in
    // order, so it takes the same number of polls; only record mode waits between them.
    private static string? WaitForState(Target target, int attempts, params string[] wanted)
    {
        string? state = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(Show(target, target.Name(SlowDeploymentName))));
            state = deployment.ProvisioningState;
            if (wanted.Contains(state) || state is State.Succeeded or State.Failed or State.Canceled)
            {
                break;
            }

            if (target.Live)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
        return state;
    }

    private static void AssertRequestBodies(SessionRecord record)
    {
        var validate = Assert.Single(Requests(record, "POST", $"/deployments/{DeploymentName}/validate"));
        var whatIf = Assert.Single(Requests(record, "POST", $"/deployments/{DeploymentName}/whatIf"));
        var create = Assert.Single(Requests(record, "PUT", $"/deployments/{DeploymentName}?"));
        foreach (var body in new[] { validate, whatIf, create })
        {
            Assert.Equal(Location, body.GetProperty("location").GetString());
            var properties = body.GetProperty("properties");
            Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
            Assert.Contains("subscriptionDeploymentTemplate", properties.GetProperty("template").GetProperty("$schema").GetString());
            Assert.Equal(Greeting, properties.GetProperty("parameters").GetProperty("greeting").GetProperty("value").GetString());
        }

        // The run-scoped-name sanitizer strips the record run's suffix from the group name.
        var slow = Assert.Single(Requests(record, "PUT", $"/deployments/{SlowDeploymentName}?"));
        Assert.Equal(
            SlowResourceGroupName,
            slow.GetProperty("properties").GetProperty("parameters").GetProperty("groupName").GetProperty("value").GetString());
    }

    // Returns the body of every recorded request that matches the method and URI, in request order.
    private static List<JsonElement> Requests(SessionRecord record, string method, string uriPart) =>
        [.. record.Entries
            .Where(e => e.Request.Method == method && e.Request.Uri.Contains(uriPart, StringComparison.OrdinalIgnoreCase))
            .Select(e =>
            {
                Assert.NotNull(e.Request.Body);
                using var document = JsonDocument.Parse(e.Request.Body);
                return document.RootElement.Clone();
            })];

    private static string[] Cli(Target target, string leaf, string deploymentName, params string[] extra) =>
        ["deployment", "sub", leaf, "--subscription-id", target.SubscriptionId, "--name", deploymentName, .. extra];

    private static Dictionary<string, object> PowerShell(Target target, string deploymentName) =>
        new() { ["SubscriptionId"] = target.SubscriptionId, ["Name"] = deploymentName };

    private static string[] WithTemplateCli(Target target, string leaf, string deploymentName, string template) =>
        Cli(target, leaf, deploymentName, "--location", Location, "--template-file", template, "--parameter", $"greeting={Greeting}");

    private static IReadOnlyList<object> WithTemplate(Target target, string leaf, string cmdlet, string deploymentName, string template) =>
        target.Run(
            WithTemplateCli(target, leaf, deploymentName, template),
            cmdlet,
            new(PowerShell(target, deploymentName))
            {
                ["Location"] = Location,
                ["TemplateFile"] = template,
                ["Parameter"] = new[] { $"greeting={Greeting}" },
            });

    private static IReadOnlyList<object> Show(Target target, string deploymentName) =>
        target.Run(Cli(target, "show", deploymentName), "Get-AzDeployment_Get", PowerShell(target, deploymentName));

    private static IReadOnlyList<object> Cancel(Target target, string deploymentName) =>
        target.Run(Cli(target, "cancel", deploymentName), "Stop-AzDeployment", PowerShell(target, deploymentName));

    private static IReadOnlyList<object> Delete(Target target, string deploymentName) =>
        target.Run(Cli(target, "delete", deploymentName), "Remove-AzDeployment", PowerShell(target, deploymentName));

    // A storage account takes long enough to provision that the deployment is still cancellable.
    // The subscription deployment creates the group, then a nested deployment adds the account to
    // it; the account name derives from the group id, so no run id appears in the template.
    private static IReadOnlyList<object> StartSlowDeployment(Target target)
    {
        string uri = $"https://management.azure.com/subscriptions/{target.SubscriptionId}/providers/Microsoft.Resources/deployments/{target.Name(SlowDeploymentName)}?api-version=2025-04-01";
        string body = """{"location":"__LOCATION__","properties":{"mode":"Incremental","parameters":{"groupName":{"value":"__GROUP__"}},"template":{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"groupName":{"type":"string"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('groupName')]","location":"__LOCATION__"},{"type":"Microsoft.Resources/deployments","apiVersion":"2022-09-01","name":"storage","resourceGroup":"[parameters('groupName')]","dependsOn":["[subscriptionResourceId('Microsoft.Resources/resourceGroups', parameters('groupName'))]"],"properties":{"mode":"Incremental","expressionEvaluationOptions":{"scope":"inner"},"template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Storage/storageAccounts","apiVersion":"2023-01-01","name":"[concat('scenario', uniqueString(resourceGroup().id))]","location":"[resourceGroup().location]","sku":{"name":"Standard_LRS"},"kind":"StorageV2"}]}}}]}}}"""
            .Replace("__LOCATION__", Location)
            .Replace("__GROUP__", target.Name(SlowResourceGroupName));
        return target.Run(
            ["rest", "--method", "PUT", "--uri", uri, "--body", body],
            "Invoke-AzRestMethod",
            new()
            {
                ["Method"] = "PUT",
                ["Uri"] = uri,
                ["Payload"] = body,
            });
    }

    /// <summary>
    /// Trims the subscription-wide deployment list to the named deployments. The list otherwise
    /// returns every deployment in the recording subscription, including other people's names and
    /// outputs. Only a JSON object with a <c>value</c> array is touched, so request bodies, URIs,
    /// and single-deployment responses pass through unchanged.
    /// </summary>
    private sealed class DeploymentListSanitizer(IReadOnlyCollection<string> keep) : IRecordingSanitizer
    {
        public string Name => "deployment-list";

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
                .Where(item => item?["name"] is JsonValue name
                               && name.TryGetValue<string>(out var deploymentName)
                               && !keep.Contains(deploymentName))
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
