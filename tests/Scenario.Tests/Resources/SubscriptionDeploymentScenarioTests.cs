using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Runs <c>deployment sub</c> through the real dispatcher, binders, and ARM pipeline. The test is
/// one recorded session that runs these parts in order:
/// <list type="bullet">
/// <item>The lifecycle runs validate, what-if, create, wait, show, list, export, the cancel
/// rejections, delete, and a cancel of an in-flight deployment. Create validates the deployment
/// before its PUT. It also covers invalid arguments, wait timeouts, a deployment that fails, a
/// create that fails validation without a PUT, and a what-if whose polled result fails.</item>
/// <item>The parameters part overlays a JSONC parameters file with inline parameters, and covers a
/// remote parameters URI, explicit validation levels, show by resource id, and a filtered list.</item>
/// <item>The what-if part covers a Modify delta, a short-circuited evaluation, and both result formats.</item>
/// <item>The template spec part deploys a spec version, then one that doesn't exist.</item>
/// <item>The Bicep part deploys a compiled Bicep file, then one whose nullable parameters are all omitted.</item>
/// </list>
/// Record mode captures the CLI workflow against Azure. Playback replays the same traffic through
/// the CLI and PowerShell surfaces and matches request bodies, so both surfaces must bind their
/// arguments into the same recorded requests. PowerShell has no what-if, wait, or export cmdlet at
/// subscription scope, so those steps run on the CLI only. On PowerShell the lifecycle's delete,
/// in-flight cancel, and in-flight delete run the real cmdlets by -Id, piped deployment, and
/// -InputObject, with and without -PassThru; they send the same requests as by name.
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
    private const string CreateFailedDeploymentName = "scenario-sub-create-failed-deployment";
    private const string CreateInvalidDeploymentName = "scenario-sub-create-invalid-deployment";
    private const string Location = "westus2";
    private const string Greeting = "hello";
    private const string Template =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"greeting":{"type":"string"}},"resources":[],"outputs":{"greeting":{"type":"string","value":"[parameters('greeting')]"}}}""";
    // This template passes the client-side checks, but ARM rejects the undeclared parameter. The
    // what-if POST returns 202 and the polled result comes back Failed; the create fails in its
    // validate POST and sends no PUT.
    private const string InvalidTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('undeclared')]","location":"westus2"}]}""";
    // ARM accepts the PUT and only fails the deployment when it evaluates the output, which
    // references a resource group that doesn't exist.
    private const string FailingTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[],"outputs":{"missing":{"type":"string","value":"[reference(subscriptionResourceId('Microsoft.Resources/resourceGroups', 'scenario-sub-missing-rg'), '2022-09-01').location]"}}}""";

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments, ExtensionUnderTest.Rest];

    private static readonly ClientType[] s_bothSurfaces = [ClientType.CLI, ClientType.PowerShell];

    [Fact]
    public void ValidateWhatIfCreateShowListExportCancelDeleteSubscriptionDeployment()
    {
        using var files = new TempFiles();
        string template = files.Write(Template);
        string invalidTemplate = files.Write(InvalidTemplate);
        string failingTemplate = files.Write(FailingTemplate);
        var parameters = new ParametersCase.Files(
            files.Write(ParametersCase.Template), files.Write(ParametersCase.UriTemplate), files.Write(ParametersCase.ParametersFile));
        var whatIf = new WhatIfCase.Files(files.Write(WhatIfCase.TaggedGroupTemplate), files.Write(WhatIfCase.ShortCircuitTemplate));
        string specParameters = files.Write(TemplateSpecCase.ParametersFile);
        // The extension picks the Bicep compiler by the .bicep extension.
        var bicep = new BicepCase.Files(files.Write(BicepCase.Template, ".bicep"), files.Write(BicepCase.NullableTemplate, ".bicep"));

        RunScenario(
            WithDeploymentList(DeploymentName, SlowDeploymentName, ParametersCase.DeploymentName).With([BicepCase.GeneratorSanitizer]),
            target =>
            {
                Lifecycle(target, template, invalidTemplate, failingTemplate);
                ParametersCase.Lifecycle(target, parameters);
                // PowerShell has no subscription what-if cmdlet.
                if (target.Surface == ClientType.CLI)
                {
                    WhatIfCase.Lifecycle(target, whatIf);
                }
                TemplateSpecCase.Lifecycle(target, specParameters);
                // The extension compiles Bicep locally before it sends the request, so playback
                // skips these steps on a machine without the Bicep CLI.
                if (target.Live || BicepCliInstalled())
                {
                    BicepCase.Lifecycle(target, bicep);
                }
            },
            record =>
            {
                AssertRequestBodies(record);
                ParametersCase.AssertRequestBodies(record);
                WhatIfCase.AssertRequestBodies(record);
                TemplateSpecCase.AssertRequestBodies(record);
                BicepCase.AssertRequestBodies(record);
            },
            target =>
            {
                foreach (var name in new[]
                {
                    DeploymentName, SlowDeploymentName, FailedDeploymentName, CreateFailedDeploymentName,
                    ParametersCase.DeploymentName, WhatIfCase.SetupDeploymentName, TemplateSpecCase.DeploymentName,
                    BicepCase.DeploymentName, BicepCase.NullableDeploymentName,
                })
                {
                    Quietly(() => Delete(target, target.Name(name)));
                }
                Quietly(() => DeleteGroup(target, SlowResourceGroupName));
                Quietly(() => DeleteGroup(target, WhatIfCase.GroupName));
                // Deleting the group deletes the template spec with it.
                Quietly(() => DeleteGroup(target, TemplateSpecCase.GroupName));
            },
            s_bothSurfaces);
    }

    // Without --name the command names the deployment after the template file or URI, so these
    // cases return a canned ARM response instead of calling Azure. A null expected name means a GUID.
    [Theory]
    [InlineData(ClientType.CLI, "main.json", null, "main")]
    [InlineData(ClientType.PowerShell, "main.json", null, "main")]
    [InlineData(ClientType.CLI, null, "https://example.com/templates/azuredeploy.json", "azuredeploy")]
    [InlineData(ClientType.PowerShell, null, "https://example.com/templates/azuredeploy.json", "azuredeploy")]
    [InlineData(ClientType.CLI, "bad name!.json", null, null)]
    [InlineData(ClientType.PowerShell, "bad name!.json", null, null)]
    public void ValidateWithoutName_NamesDeploymentAfterTemplate(ClientType surface, string? templateFileName, string? templateUri, string? expectedName)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            List<string> cli = ["deployment", "sub", "validate", "--subscription-id", SubscriptionId, "--location", Location];
            var powerShell = new Dictionary<string, object> { ["SubscriptionId"] = SubscriptionId, ["Location"] = Location };
            if (templateFileName is not null)
            {
                string template = Path.Combine(directory, templateFileName);
                File.WriteAllText(template, Template);
                cli.AddRange(["--template-file", template, "--parameter", $"greeting={Greeting}"]);
                powerShell["TemplateFile"] = template;
                powerShell["Parameter"] = new[] { $"greeting={Greeting}" };
            }
            else
            {
                cli.AddRange(["--template-uri", templateUri!, "--query-string", "sig=abc"]);
                powerShell["TemplateUri"] = templateUri!;
                powerShell["QueryString"] = "sig=abc";
            }

            var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$$"""{"properties":{"provisioningState":"{{{State.Succeeded}}}"}}""", Encoding.UTF8, "application/json"),
            });
            using (var host = new ScenarioHost(surface, http, extensions: s_extensions))
            {
                new Target(host, surface, SubscriptionId, Live: false).Run([.. cli], "Test-AzDeployment", powerShell);
            }

            var request = http.Captured[0];
            const string Prefix = $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments/";
            const string Suffix = "/validate?api-version=2025-04-01";
            Assert.Equal("POST", request.Method);
            Assert.StartsWith(Prefix, request.Uri);
            Assert.EndsWith(Suffix, request.Uri);
            string name = request.Uri[Prefix.Length..^Suffix.Length];
            if (expectedName is null)
            {
                Assert.True(Guid.TryParse(name, out _), $"Expected a generated GUID deployment name, got '{name}'.");
            }
            else
            {
                Assert.Equal(expectedName, name);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Record mode runs the lifecycle once on the CLI against Azure, then cleans up. Playback
    // replays the recording once per surface.
    private static void RunScenario(
        RecordingSanitizers sanitizers,
        Action<Target> lifecycle,
        Action<SessionRecord> assertRequestBodies,
        Action<Target> cleanup,
        params ClientType[] playbackSurfaces)
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(SubscriptionDeploymentScenarioTests), RecordingName, new DefaultRequestMatcher(matchBody: true), sanitizers);

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
                lifecycle(target);
                assertRequestBodies(recording.Record);
                recording.Save();
            }
            finally
            {
                // The cleanup runs after the host saves the recording, so the recording never includes it.
                cleanup(target);
            }
            return;
        }

        // Body matching already proves each surface sent exactly the recorded requests. These
        // assertions check the values inside those requests.
        assertRequestBodies(recording.Record);

        foreach (var surface in playbackSurfaces)
        {
            using var host = new ScenarioHost(surface, recording.CreateInterceptor(), extensions: s_extensions);
            lifecycle(new Target(host, surface, SubscriptionId, Live: false));
        }
    }

    // Trims every deployment list to the named deployments. Runs before the run-scoped-name
    // sanitizer, so only this run's deployments survive.
    private static RecordingSanitizers WithDeploymentList(params string[] deploymentNames)
    {
        var mode = RecorderModeResolver.FromEnvironment();
        var listTrim = new DeploymentListSanitizer([.. deploymentNames.Select(name => TestRun.ScopedName(name, mode))]);
        return new RecordingSanitizers(
            [listTrim, .. RecordingSanitizers.Default.Sanitizers],
            [.. RecordingSanitizers.Default.RemovedHeaders]);
    }

    private static void Quietly(Action cleanupStep)
    {
        try { cleanupStep(); } catch { }
    }

    // A Target is one surface. Run picks the CLI or PowerShell form of a command. Name scopes a
    // base name to the record run, and the run-scoped-name sanitizer strips the suffix from the recording.
    private sealed record Target(ScenarioHost Host, ClientType Surface, string SubscriptionId, bool Live)
    {
        public string Name(string baseName) => Live ? TestRun.ScopedName(baseName, RecorderMode.Record) : baseName;

        public IReadOnlyList<object> Run(string[] cli, string cmdlet, Dictionary<string, object> powerShell) =>
            Surface == ClientType.CLI ? Host.RunCli(cli) : Host.RunPowerShell(cmdlet, powerShell);
    }

    // Writes the template files the steps read, and deletes them when the test ends.
    private sealed class TempFiles : IDisposable
    {
        private readonly List<string> _paths = [];

        public string Write(string content, string extension = ".json")
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
            File.WriteAllText(path, content);
            _paths.Add(path);
            return path;
        }

        public void Dispose() => _paths.ForEach(File.Delete);
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

    private static void Lifecycle(Target target, string template, string invalidTemplate, string failingTemplate)
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

        if (target.Surface == ClientType.PowerShell)
        {
            // Same DELETE as by name: -Id selects the subscription and deployment, and -PassThru writes True.
            Assert.True(Assert.IsType<bool>(Assert.Single(target.Host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(
                new Dictionary<string, object> { ["Id"] = DeploymentId(target, name), ["PassThru"] = true }))));
        }
        else
        {
            Assert.Empty(Delete(target, name));
        }

        if (target.Surface == ClientType.CLI)
        {
            Assert.Empty(target.Host.RunCli(Cli(target, "wait", name, "--deleted", "--interval", "1")));
        }

        Assert.DoesNotContain(name, ListNames(target));

        var canceled = CancelInFlightDeployment(target);

        if (target.Surface == ClientType.PowerShell)
        {
            // -InputObject takes the deployment's Id; without -PassThru nothing is written.
            Assert.Empty(target.Host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(
                new Dictionary<string, object> { ["InputObject"] = canceled }));
        }
        else
        {
            Assert.Empty(Delete(target, target.Name(SlowDeploymentName)));
        }

        AssertInvalidArgumentsFail(target, template);

        AssertCreateFailureReportsArmError(target, failingTemplate);

        AssertCreateValidationFailureSendsNoPut(target, invalidTemplate);

        if (target.Surface == ClientType.CLI)
        {
            AssertWaitTimesOut(target);
            AssertWaitOnFailedDeploymentFails(target);
            AssertWhatIfPolledFailure(target, invalidTemplate);
        }
    }

    // These throw before the command sends any request. Playback would fail on an unrecorded
    // request, so a passing run also proves nothing reached ARM.
    private static void AssertInvalidArgumentsFail(Target target, string template)
    {
        string name = target.Name(MissingDeploymentName);

        // The CLI fails in argument binding and PowerShell fails in the processor. Both are client-side errors.
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
            // Only PowerShell can show, stop, or remove a deployment by id.
            string groupScopedId = $"/subscriptions/{target.SubscriptionId}/resourceGroups/group/providers/Microsoft.Resources/deployments/{name}";
            var wrongScope = Assert.Throws<InvalidInputException>(() => target.Host.RunPowerShell(
                "Get-AzDeployment_GetById",
                new Dictionary<string, object> { ["Id"] = groupScopedId }));
            Assert.Contains("Invalid deployment id", wrongScope.Message);

            var byId = new Dictionary<string, object> { ["Id"] = groupScopedId };
            var stop = Assert.ThrowsAny<Exception>(() => target.Host.RunPowerShellCmdlet<StopDeploymentCmdlet>(byId));
            var remove = Assert.ThrowsAny<Exception>(() => target.Host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(byId));
            foreach (var error in new[] { stop, remove })
            {
                Assert.Contains("Invalid deployment id", error.Message);
                Assert.Contains("/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}", error.Message);
            }
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

    // A failed subscription deployment reports the ARM error on both surfaces. Playback fails on
    // any unrecorded request, so a passing run also proves the deployment GET and the operations
    // list were sent.
    private static void AssertCreateFailureReportsArmError(Target target, string failingTemplate)
    {
        string name = target.Name(CreateFailedDeploymentName);

        var error = Assert.Throws<InvalidOperationException>(() => target.Run(
            Cli(target, "create", name, "--location", Location, "--template-file", failingTemplate),
            "New-AzDeployment",
            new(PowerShell(target, name)) { ["Location"] = Location, ["TemplateFile"] = failingTemplate }));
        Assert.StartsWith($"Deployment '{name}' failed.", error.Message);
        Assert.Contains("DeploymentFailed", error.Message);
        Assert.Contains("ResourceGroupNotFound", error.Message);
        // ARM's DeploymentFailed message carries a correlation id too, so check the appended last line.
        Assert.Matches(@"^Correlation ID: \S+$", error.Message.Split('\n')[^1].Trim());
        Assert.Contains("Long running operation failed", error.InnerException?.Message);

        Assert.Empty(Delete(target, name));
    }

    // T2, spec scenario "T2 Invalid template fails validation without a PUT". Nothing is created,
    // so there is no cleanup; AssertRequestBodies checks that no PUT was recorded.
    private static void AssertCreateValidationFailureSendsNoPut(Target target, string invalidTemplate)
    {
        string name = target.Name(CreateInvalidDeploymentName);

        var error = Assert.Throws<InvalidOperationException>(() => target.Run(
            Cli(target, "create", name, "--location", Location, "--template-file", invalidTemplate),
            "New-AzDeployment",
            new(PowerShell(target, name)) { ["Location"] = Location, ["TemplateFile"] = invalidTemplate }));
        Assert.StartsWith("Deployment validation failed.", error.Message);
        Assert.Contains("InvalidTemplate", error.Message);
    }

    // T1, spec scenario "T1 Create validates the deployment before the PUT": the lifecycle's
    // standalone validate comes first, then the create's validate, then its PUT with the same body.
    private static void AssertCreateValidatesBeforePut(SessionRecord record)
    {
        string validateUri = $"/deployments/{DeploymentName}/validate";
        string putUri = $"/deployments/{DeploymentName}?";
        int put = record.Entries.FindIndex(e => IsRequest(e, "PUT", putUri));
        int createValidate = record.Entries.FindLastIndex(e => IsRequest(e, "POST", validateUri));
        Assert.Equal(2, record.Entries.Count(e => IsRequest(e, "POST", validateUri)));
        Assert.InRange(createValidate, 0, put - 1);

        var validateBody = Requests(record, "POST", validateUri)[^1];
        var putBody = Assert.Single(Requests(record, "PUT", putUri));
        Assert.True(JsonElement.DeepEquals(putBody.GetProperty("location"), validateBody.GetProperty("location")));
        Assert.True(JsonElement.DeepEquals(putBody.GetProperty("properties"), validateBody.GetProperty("properties")));

        string invalid = $"/deployments/{CreateInvalidDeploymentName}";
        Assert.Single(Requests(record, "POST", $"{invalid}/validate"));
        Assert.Empty(Requests(record, "PUT", $"{invalid}?"));
    }

    private static bool IsRequest(SessionRecordEntry entry, string method, string uriPart) =>
        entry.Request.Method == method && entry.Request.Uri.Contains(uriPart, StringComparison.OrdinalIgnoreCase);

    private static void AssertFailureFollowUpRequests(SessionRecord record)
    {
        string deployment = $"/deployments/{CreateFailedDeploymentName}";
        Assert.Single(record.Entries, e => e.Request.Method == "GET" && e.Request.Uri.Contains($"{deployment}?", StringComparison.OrdinalIgnoreCase));
        Assert.Single(record.Entries, e => e.Request.Method == "GET" && e.Request.Uri.Contains($"{deployment}/operations?", StringComparison.OrdinalIgnoreCase));
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

    // Returns the canceled deployment.
    private static DeploymentExtended CancelInFlightDeployment(Target target)
    {
        // `deployment sub create` blocks until the deployment is terminal, so start the
        // deployment to cancel with a raw PUT that returns as soon as ARM accepts it.
        var response = Assert.IsType<RestResponse>(Assert.Single(StartSlowDeployment(target)));
        Assert.Equal(201, response.StatusCode);

        var inFlight = WaitForState(target, attempts: 15, State.Accepted, State.Running);
        string? state = inFlight.ProvisioningState;
        Assert.True(state is State.Accepted or State.Running, $"The deployment to cancel is '{state}', not cancellable; re-record this test.");

        try
        {
            if (target.Surface == ClientType.PowerShell)
            {
                // Same cancel POST as by name: the piped deployment's Id selects it, and -PassThru writes True.
                Assert.True(Assert.IsType<bool>(Assert.Single(target.Host.RunPowerShellCmdlet<StopDeploymentCmdlet>(
                    new Dictionary<string, object> { ["PassThru"] = true }, pipelineInput: inFlight))));
            }
            else
            {
                Assert.Empty(Cancel(target, target.Name(SlowDeploymentName)));
            }
        }
        catch (HttpRequestException error) when (error.Message.Contains("DeploymentCannotBeCancelled"))
        {
            Assert.Fail("The deployment finished before it could be cancelled; re-record this test.");
        }

        var canceled = WaitForState(target, attempts: 30, State.Canceled);
        Assert.Equal(State.Canceled, canceled.ProvisioningState);
        return canceled;
    }

    private static void AssertCancelFails(string expectedCode, Target target, string deploymentName)
    {
        var error = Assert.Throws<HttpRequestException>(() => Cancel(target, deploymentName));
        Assert.Contains(expectedCode, error.Message);
    }

    // Stops at a wanted state or any terminal one. Playback replays the recorded responses in
    // order, so it takes the same number of polls; only record mode waits between them.
    private static DeploymentExtended WaitForState(Target target, int attempts, params string[] wanted)
    {
        DeploymentExtended? deployment = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            deployment = Assert.IsType<DeploymentExtended>(Assert.Single(Show(target, target.Name(SlowDeploymentName))));
            string? state = deployment.ProvisioningState;
            if (wanted.Contains(state) || state is State.Succeeded or State.Failed or State.Canceled)
            {
                break;
            }

            if (target.Live)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
        return Assert.IsType<DeploymentExtended>(deployment);
    }

    private static void AssertRequestBodies(SessionRecord record)
    {
        // Create validates too; the first validate POST is the standalone validate step.
        var validate = Requests(record, "POST", $"/deployments/{DeploymentName}/validate")[0];
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

        AssertFailureFollowUpRequests(record);
        AssertCreateValidatesBeforePut(record);
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

    private static string DeploymentId(Target target, string deploymentName) =>
        $"/subscriptions/{target.SubscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}";

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
        return Put(target, uri, body);
    }

    private static IReadOnlyList<object> Put(Target target, string uri, string body) =>
        target.Run(
            ["rest", "--method", "PUT", "--uri", uri, "--body", body],
            "Invoke-AzRestMethod",
            new()
            {
                ["Method"] = "PUT",
                ["Uri"] = uri,
                ["Payload"] = body,
            });

    // Only record-mode cleanup calls this, so it always runs on the CLI.
    private static IReadOnlyList<object> DeleteGroup(Target target, string baseName) =>
        target.Host.RunCli("group", "delete", "--subscription-id", target.SubscriptionId, "--name", target.Name(baseName));

    // Covers a JSONC parameters file overlaid by inline parameters, a remote
    // parameters URI, explicit validation levels, show by resource id, and a filtered list. Both
    // surfaces must merge the file and inline parameters into the same request. PowerShell has no
    // subscription what-if and no list filter, so those steps run on the CLI only.
    private static class ParametersCase
    {
        public const string DeploymentName = "scenario-sub-params";
        private const string UriDeploymentName = "scenario-sub-params-uri";
        private const string OverlayGreeting = "overlay";
        private const string FileGroupName = "file-group";
        // The URI pins a commit, so the parameters the deployment resolves never change between re-records.
        private const string ParametersUri =
            "https://raw.githubusercontent.com/Azure/azure-cli/5c86afc13482f32128dd94e03544b6844ac77f92/src/azure-cli/azure/cli/command_modules/resource/tests/latest/subscription_level_parameters.json";
        private const string ParametersUriGroupName = "cli_test_subscription_level_deployment";
        public const string Template =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"greeting":{"type":"string"},"nestedRGName":{"type":"string"}},"resources":[],"outputs":{"greeting":{"type":"string","value":"[parameters('greeting')]"},"nestedRGName":{"type":"string","value":"[parameters('nestedRGName')]"}}}""";
        // Declares only the parameter the pinned parameters URI supplies.
        public const string UriTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"nestedRGName":{"type":"string"}},"resources":[],"outputs":{"nestedRGName":{"type":"string","value":"[parameters('nestedRGName')]"}}}""";
        // The comments and trailing comma check that the builder accepts JSONC parameters files. The
        // inline greeting overrides the file's greeting.
        public const string ParametersFile =
            """
            {
              // Parameters for the scenario deployment.
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                /* Overridden by the inline greeting. */
                "greeting": { "value": "from-file" },
                "nestedRGName": { "value": "file-group" }, // kept from the file
              }
            }
            """;

        public sealed record Files(string Template, string UriTemplate, string Parameters);

        public static void Lifecycle(Target target, Files files)
        {
            string name = target.Name(DeploymentName);

            ValidateWithParametersFile(target, files, name);

            if (target.Surface == ClientType.CLI)
            {
                WhatIfWithValidationLevel(target, files, name);
            }

            CreateWithParametersFile(target, files, name);

            ShowById(target, name);

            if (target.Surface == ClientType.CLI)
            {
                ListWithFilter(target, name);
            }

            ValidateWithParametersUri(target, files);

            Assert.Empty(Delete(target, name));
        }

        // Validates a JSONC parameters file overlaid by an inline parameter at the Template
        // level. The inline greeting wins, and the file supplies nestedRGName.
        private static void ValidateWithParametersFile(Target target, Files files, string name)
        {
            var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(target.Run(
                Cli(target, "validate", name, "--location", Location, "--template-file", files.Template,
                    "--parameters-file", files.Parameters, "--parameter", $"greeting={OverlayGreeting}", "--validation-level", "Template"),
                "Test-AzDeployment",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.Template,
                    ["ParametersFile"] = files.Parameters,
                    ["Parameter"] = new[] { $"greeting={OverlayGreeting}" },
                    ["ValidationLevel"] = "Template",
                })));
            Assert.Null(validation.Error);
            Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);
            Assert.Equal(OverlayGreeting, validation.Properties?.Parameters?["greeting"].Value);
            Assert.Equal(FileGroupName, validation.Properties?.Parameters?["nestedRGName"].Value);
        }

        // Checks that what-if sends the ProviderNoRbac validation level. This step runs on the CLI only.
        private static void WhatIfWithValidationLevel(Target target, Files files, string name)
        {
            var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Host.RunCli(
                Cli(target, "what-if", name, "--location", Location, "--template-file", files.Template,
                    "--parameters-file", files.Parameters, "--parameter", $"greeting={OverlayGreeting}", "--validation-level", "ProviderNoRbac"))));
            Assert.Equal(State.Succeeded, whatIf.Status);
            Assert.Null(whatIf.Error);
            Assert.Empty(whatIf.Properties?.Changes ?? []);
        }

        // Creates with the overlaid parameters file at the Provider validation level, and checks
        // that the outputs carry the merged values.
        private static void CreateWithParametersFile(Target target, Files files, string name)
        {
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(target.Run(
                Cli(target, "create", name, "--location", Location, "--template-file", files.Template,
                    "--parameters-file", files.Parameters, "--parameter", $"greeting={OverlayGreeting}", "--validation-level", "Provider"),
                "New-AzDeployment",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.Template,
                    ["ParametersFile"] = files.Parameters,
                    ["Parameter"] = new[] { $"greeting={OverlayGreeting}" },
                    ["ValidationLevel"] = "Provider",
                })));
            AssertMergedOutputs(name, deployment);
        }

        // Show on the CLI; PowerShell resolves the same GET from the deployment's resource id.
        private static void ShowById(Target target, string name)
        {
            string id = $"/subscriptions/{target.SubscriptionId}/providers/Microsoft.Resources/deployments/{name}";
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(target.Run(
                Cli(target, "show", name),
                "Get-AzDeployment_GetById",
                new() { ["Id"] = id })));
            AssertMergedOutputs(name, deployment);
            Assert.Equal(id, deployment.Id, ignoreCase: true);
        }

        // Lists with an OData filter. Every returned deployment must match it, and this run's
        // deployment must be among them. This step runs on the CLI only, because
        // Get-AzDeployment_List has no filter.
        private static void ListWithFilter(Target target, string name)
        {
            var deployments = Assert.IsType<List<DeploymentExtended>>(Assert.Single(target.Host.RunCli(
                "deployment", "sub", "list", "--subscription-id", target.SubscriptionId, "--filter", "provisioningState eq 'Succeeded'")));
            Assert.Contains(name, deployments.Select(d => d.Name));
            Assert.All(deployments, d => Assert.Equal(State.Succeeded, d.ProvisioningState));
        }

        // Sends a remote parameters file as a link, and ARM resolves the parameter from it.
        private static void ValidateWithParametersUri(Target target, Files files)
        {
            string name = target.Name(UriDeploymentName);
            var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(target.Run(
                Cli(target, "validate", name, "--location", Location, "--template-file", files.UriTemplate, "--parameters-uri", ParametersUri),
                "Test-AzDeployment",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.UriTemplate,
                    ["ParametersUri"] = ParametersUri,
                })));
            Assert.Null(validation.Error);
            Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);
            Assert.Equal(ParametersUriGroupName, validation.Properties?.Parameters?["nestedRGName"].Value);
        }

        private static void AssertMergedOutputs(string name, DeploymentExtended deployment)
        {
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(OverlayGreeting, deployment.Properties?.Outputs?["greeting"].Value);
            Assert.Equal(FileGroupName, deployment.Properties?.Outputs?["nestedRGName"].Value);
        }

        public static void AssertRequestBodies(SessionRecord record)
        {
            var bodies = new Dictionary<string, JsonElement>
            {
                // The create validates too; the first validate POST is the standalone validate step.
                ["Template"] = Requests(record, "POST", $"/deployments/{DeploymentName}/validate")[0],
                ["ProviderNoRbac"] = Assert.Single(Requests(record, "POST", $"/deployments/{DeploymentName}/whatIf")),
                ["Provider"] = Assert.Single(Requests(record, "PUT", $"/deployments/{DeploymentName}?")),
            };
            foreach (var (level, body) in bodies)
            {
                Assert.Equal(Location, body.GetProperty("location").GetString());
                var properties = body.GetProperty("properties");
                Assert.Equal(level, properties.GetProperty("validationLevel").GetString());
                var parameters = properties.GetProperty("parameters");
                Assert.Equal(OverlayGreeting, parameters.GetProperty("greeting").GetProperty("value").GetString());
                Assert.Equal(FileGroupName, parameters.GetProperty("nestedRGName").GetProperty("value").GetString());
                Assert.False(properties.TryGetProperty("parametersLink", out _));
            }

            var uriProperties = Assert.Single(Requests(record, "POST", $"/deployments/{UriDeploymentName}/validate")).GetProperty("properties");
            Assert.Equal(ParametersUri, uriProperties.GetProperty("parametersLink").GetProperty("uri").GetString());
            Assert.False(uriProperties.TryGetProperty("parameters", out _));
            Assert.False(uriProperties.TryGetProperty("validationLevel", out _));

            // The lifecycle's unfiltered lists share this URI, so check that exactly one list sent the filter.
            Assert.Single(record.Entries, e => e.Request.Method == "GET"
                && e.Request.Uri.Contains("/providers/Microsoft.Resources/deployments?", StringComparison.OrdinalIgnoreCase)
                && Uri.UnescapeDataString(e.Request.Uri).Contains("filter=provisioningState", StringComparison.Ordinal));
        }
    }

    // Checks what what-if reports against existing state. A deployment first
    // creates a tagged resource group that the what-if calls compare against. These steps run on
    // the CLI only.
    private static class WhatIfCase
    {
        public const string SetupDeploymentName = "scenario-sub-whatif-setup";
        private const string ModifyName = "scenario-sub-whatif-modify";
        private const string ShortCircuitName = "scenario-sub-whatif-short-circuit";
        private const string CreateName = "scenario-sub-whatif-create";
        private const string ResourceIdOnlyName = "scenario-sub-whatif-ids";
        public const string GroupName = "scenario-sub-whatif-rg";
        private const string NewGroupName = "scenario-sub-whatif-new-rg";
        private const string NestedDeploymentName = "scenario-sub-whatif-nested";
        public const string TaggedGroupTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"groupName":{"type":"string"},"tagValue":{"type":"string"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('groupName')]","location":"westus2","tags":{"a":"[parameters('tagValue')]"}}]}""";
        // random defaults to a utcNow() comparison, which what-if can't evaluate. So the conditional
        // group becomes a potential change, the group whose name depends on random is unsupported,
        // and the nested deployment that reads random is short-circuited.
        public const string ShortCircuitTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"groupName":{"type":"string"},"random":{"type":"bool","defaultValue":"[greater(int(utcNow('%f')), 4)]"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('groupName')]","location":"westus2","condition":"[parameters('random')]","tags":{"a":"three"}},{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[format('{0}-{1}', parameters('groupName'), if(parameters('random'), 'two', 'three'))]","location":"westus2"},{"type":"Microsoft.Resources/deployments","apiVersion":"2024-11-01","name":"scenario-sub-whatif-nested","location":"[deployment().location]","properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[],"variables":{"undecidable":"[parameters('random')]"}}}}]}""";

        public sealed record Files(string TaggedGroup, string ShortCircuit);

        public static void Lifecycle(Target target, Files files)
        {
            string group = target.Name(GroupName);
            var setup = Assert.IsType<DeploymentExtended>(Assert.Single(target.Host.RunCli(
                TaggedGroup(target, "create", target.Name(SetupDeploymentName), files, group, "one"))));
            Assert.Equal(State.Succeeded, setup.ProvisioningState);

            WhatIfModify(target, files, group);
            WhatIfShortCircuit(target, files, group);
            WhatIfCreateInBothFormats(target, files);

            Assert.Empty(Delete(target, target.Name(SetupDeploymentName)));
        }

        // Changes the group's tag, which what-if reports as a Modify with a delta from the old
        // value to the new one.
        private static void WhatIfModify(Target target, Files files, string group)
        {
            var result = WhatIf(TaggedGroup(target, "what-if", target.Name(ModifyName), files, group, "two"), target);
            var change = Assert.Single(result.Properties?.Changes ?? []);
            Assert.Equal("Modify", change.ChangeType);
            Assert.EndsWith($"/resourceGroups/{group}", change.ResourceId, StringComparison.OrdinalIgnoreCase);
            AssertTagDelta(change, "one", "two");
        }

        // What-if reports the conditional group as a potential change, the group whose name
        // it can't calculate as unsupported, and the nested deployment as short-circuited.
        private static void WhatIfShortCircuit(Target target, Files files, string group)
        {
            var result = WhatIf(
                Cli(target, "what-if", target.Name(ShortCircuitName), "--location", Location, "--template-file", files.ShortCircuit,
                    "--parameter", $"groupName={group}"),
                target);

            var unsupported = Assert.Single(result.Properties?.Changes ?? [], c => c.ChangeType == "Unsupported");
            Assert.False(string.IsNullOrWhiteSpace(unsupported.UnsupportedReason));

            var potential = Assert.Single(
                result.Properties?.PotentialChanges ?? [],
                c => c.ResourceId?.EndsWith($"/resourceGroups/{group}", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Equal("Modify", potential.ChangeType);
            AssertTagDelta(potential, "one", "three");

            Assert.NotNull(result.Properties?.Diagnostics);
            var diagnostics = JsonSerializer.SerializeToElement(result.Properties.Diagnostics);
            Assert.Contains(
                diagnostics.EnumerateArray(),
                d => d.GetProperty("code").GetString() == "NestedDeploymentShortCircuited"
                     && d.GetProperty("level").GetString() == "Warning"
                     && d.GetProperty("target").GetString()!.EndsWith(
                         $"/providers/Microsoft.Resources/deployments/{NestedDeploymentName}", StringComparison.OrdinalIgnoreCase));
        }

        // What-if reports a group that doesn't exist yet as a Create. The FullResourcePayloads
        // format includes the resource payload, and ResourceIdOnly includes only the id.
        private static void WhatIfCreateInBothFormats(Target target, Files files)
        {
            string group = target.Name(NewGroupName);

            var full = Assert.Single(WhatIf(TaggedGroup(target, "what-if", target.Name(CreateName), files, group, "one"), target).Properties?.Changes ?? []);
            Assert.Equal("Create", full.ChangeType);
            Assert.EndsWith($"/resourceGroups/{group}", full.ResourceId, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(full.After);
            Assert.Null(full.Before);

            var idOnly = Assert.Single(WhatIf(
                [.. TaggedGroup(target, "what-if", target.Name(ResourceIdOnlyName), files, group, "one"), "--result-format", "ResourceIdOnly"],
                target).Properties?.Changes ?? []);
            Assert.Equal("Create", idOnly.ChangeType);
            Assert.EndsWith($"/resourceGroups/{group}", idOnly.ResourceId, StringComparison.OrdinalIgnoreCase);
            Assert.Null(idOnly.Before);
            Assert.Null(idOnly.After);
            Assert.Null(idOnly.Delta);
        }

        private static WhatIfOperationResult WhatIf(string[] cli, Target target)
        {
            var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Host.RunCli(cli)));
            Assert.Equal(State.Succeeded, result.Status);
            Assert.Null(result.Error);
            return result;
        }

        // ARM may report the tag as one tags.a entry or as a tags entry with an a child, so the
        // test flattens the delta to full paths before the lookup.
        private static void AssertTagDelta(WhatIfChange change, string before, string after)
        {
            var delta = Flatten(change.Delta, prefix: null).ToList();
            var tag = Assert.Single(delta, d => d.Path == "tags.a");
            Assert.Equal("Modify", tag.Change.PropertyChangeType);
            Assert.Equal(before, JsonSerializer.SerializeToElement(tag.Change.Before).GetString());
            Assert.Equal(after, JsonSerializer.SerializeToElement(tag.Change.After).GetString());
        }

        private static IEnumerable<(string Path, WhatIfPropertyChange Change)> Flatten(List<WhatIfPropertyChange>? changes, string? prefix)
        {
            foreach (var change in changes ?? [])
            {
                string path = prefix is null ? change.Path ?? "" : $"{prefix}.{change.Path}";
                if (change.Children is { Count: > 0 })
                {
                    foreach (var child in Flatten(change.Children, path))
                    {
                        yield return child;
                    }
                }
                else
                {
                    yield return (path, change);
                }
            }
        }

        public static void AssertRequestBodies(SessionRecord record)
        {
            var expected = new Dictionary<string, (string Group, string Tag, string Format)>
            {
                [ModifyName] = (GroupName, "two", "FullResourcePayloads"),
                [CreateName] = (NewGroupName, "one", "FullResourcePayloads"),
                [ResourceIdOnlyName] = (NewGroupName, "one", "ResourceIdOnly"),
            };
            foreach (var (name, (group, tag, format)) in expected)
            {
                var properties = Assert.Single(Requests(record, "POST", $"/deployments/{name}/whatIf")).GetProperty("properties");
                Assert.Equal(format, properties.GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
                var parameters = properties.GetProperty("parameters");
                // The run-scoped-name sanitizer strips the record run's suffix from the group name.
                Assert.Equal(group, parameters.GetProperty("groupName").GetProperty("value").GetString());
                Assert.Equal(tag, parameters.GetProperty("tagValue").GetProperty("value").GetString());
            }

            var shortCircuit = Assert.Single(Requests(record, "POST", $"/deployments/{ShortCircuitName}/whatIf")).GetProperty("properties");
            Assert.Equal(GroupName, shortCircuit.GetProperty("parameters").GetProperty("groupName").GetProperty("value").GetString());
            Assert.False(shortCircuit.GetProperty("parameters").TryGetProperty("random", out _));
        }

        private static string[] TaggedGroup(Target target, string leaf, string deploymentName, Files files, string group, string tagValue) =>
            Cli(target, leaf, deploymentName, "--location", Location, "--template-file", files.TaggedGroup,
                "--parameter", $"groupName={group}", "--parameter", $"tagValue={tagValue}");
    }

    // Validates, runs what-if on, creates, shows, and exports a deployment whose
    // template comes from a template spec version. A final create references a version that
    // doesn't exist. The setup creates a resource group and the spec with raw PUTs.
    private static class TemplateSpecCase
    {
        public const string DeploymentName = "scenario-sub-ts-deployment";
        private const string MissingVersionDeploymentName = "scenario-sub-ts-missing-version";
        public const string GroupName = "scenario-sub-ts-rg";
        private const string SpecName = "scenario-sub-ts-spec";
        private const string SpecApiVersion = "2022-02-01";
        private const string Greeting = "from-spec";
        private const string MainTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"greeting":{"type":"string"}},"resources":[],"outputs":{"greeting":{"type":"string","value":"[parameters('greeting')]"}}}""";
        public const string ParametersFile =
            """{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#","contentVersion":"1.0.0.0","parameters":{"greeting":{"value":"from-spec"}}}""";

        public static void Lifecycle(Target target, string parameters)
        {
            CreateTemplateSpec(target);

            string name = target.Name(DeploymentName);
            string specId = SpecId(target, "v1");

            // Validates the spec version with a parameters file.
            var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(
                WithSpec(target, "validate", "Test-AzDeployment", name, specId, parameters)));
            Assert.Null(validation.Error);
            Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);

            // What-if of a spec with no resources reports no changes. This step runs on the CLI only.
            if (target.Surface == ClientType.CLI)
            {
                var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Host.RunCli(WithSpecCli(target, "what-if", name, specId, parameters))));
                Assert.Equal(State.Succeeded, whatIf.Status);
                Assert.Null(whatIf.Error);
                Assert.Empty(whatIf.Properties?.Changes ?? []);
            }

            // Creates from the spec, and the deployment links back to the spec version.
            AssertDeployedFromSpec(name, specId, WithSpec(target, "create", "New-AzDeployment", name, specId, parameters));
            AssertDeployedFromSpec(name, specId, Show(target, name));

            if (target.Surface == ClientType.CLI)
            {
                var exported = Assert.IsType<JsonElement>(Assert.Single(target.Host.RunCli(Cli(target, "export", name))));
                Assert.Equal(JsonValueKind.Object, exported.ValueKind);
                Assert.True(exported.GetProperty("outputs").TryGetProperty("greeting", out _), $"Exported template has no greeting output: {exported}");
            }

            // A create from a spec version that doesn't exist fails with the ARM error.
            string missingName = target.Name(MissingVersionDeploymentName);
            var error = Assert.ThrowsAny<Exception>(() => WithSpec(target, "create", "New-AzDeployment", missingName, SpecId(target, "v2"), parameters));
            Assert.Contains("InvalidTemplateSpec", error.ToString());

            Assert.Empty(Delete(target, name));
        }

        private static string SpecId(Target target, string version) =>
            $"/subscriptions/{target.SubscriptionId}/resourceGroups/{target.Name(GroupName)}/providers/Microsoft.Resources/templateSpecs/{SpecName}/versions/{version}";

        private static void AssertDeployedFromSpec(string name, string specId, IReadOnlyList<object> outputs)
        {
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(outputs));
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(Greeting, deployment.Properties?.Outputs?["greeting"].Value);
            Assert.Equal(specId, deployment.Properties?.TemplateLink?.Id, ignoreCase: true);
        }

        // The group holds the template spec. No command creates the spec or its version, so this
        // method creates them with raw PUTs.
        private static void CreateTemplateSpec(Target target)
        {
            string group = target.Name(GroupName);
            Assert.Single(target.Run(
                ["group", "create", "--subscription-id", target.SubscriptionId, "--name", group, "--location", Location],
                "New-AzResourceGroup",
                new()
                {
                    ["SubscriptionId"] = target.SubscriptionId,
                    ["Name"] = group,
                    ["Location"] = Location,
                }));

            string spec = $"https://management.azure.com/subscriptions/{target.SubscriptionId}/resourceGroups/{group}/providers/Microsoft.Resources/templateSpecs/{SpecName}";
            AssertPutSucceeded(Put(target, $"{spec}?api-version={SpecApiVersion}", $$$"""{"location":"{{{Location}}}","properties":{}}"""));
            AssertPutSucceeded(Put(target, $"{spec}/versions/v1?api-version={SpecApiVersion}", $$$"""{"location":"{{{Location}}}","properties":{"mainTemplate":{{{MainTemplate}}}}}"""));
        }

        private static void AssertPutSucceeded(IReadOnlyList<object> outputs)
        {
            var response = Assert.IsType<RestResponse>(Assert.Single(outputs));
            Assert.True(response.StatusCode is 200 or 201, $"PUT returned {response.StatusCode}.");
        }

        public static void AssertRequestBodies(SessionRecord record)
        {
            // The create validates too; the first validate POST is the standalone validate step.
            var validate = Requests(record, "POST", $"/deployments/{DeploymentName}/validate")[0];
            var whatIf = Assert.Single(Requests(record, "POST", $"/deployments/{DeploymentName}/whatIf"));
            var create = Assert.Single(Requests(record, "PUT", $"/deployments/{DeploymentName}?"));
            foreach (var body in new[] { validate, whatIf, create })
            {
                AssertSpecBody(body, "v1");
            }

            // The missing version fails in the create's validate POST, so no PUT is sent.
            AssertSpecBody(Assert.Single(Requests(record, "POST", $"/deployments/{MissingVersionDeploymentName}/validate")), "v2");
            Assert.Empty(Requests(record, "PUT", $"/deployments/{MissingVersionDeploymentName}?"));
        }

        // The run-scoped-name sanitizer strips the record run's suffix from the group in the spec id.
        private static void AssertSpecBody(JsonElement body, string version)
        {
            Assert.Equal(Location, body.GetProperty("location").GetString());
            var properties = body.GetProperty("properties");
            Assert.False(properties.TryGetProperty("template", out _));
            Assert.EndsWith(
                $"/resourceGroups/{GroupName}/providers/Microsoft.Resources/templateSpecs/{SpecName}/versions/{version}",
                properties.GetProperty("templateLink").GetProperty("id").GetString());
            Assert.Equal(Greeting, properties.GetProperty("parameters").GetProperty("greeting").GetProperty("value").GetString());
        }

        private static string[] WithSpecCli(Target target, string leaf, string deploymentName, string specId, string parameters) =>
            Cli(target, leaf, deploymentName, "--location", Location, "--template-spec-id", specId, "--parameters-file", parameters);

        private static IReadOnlyList<object> WithSpec(Target target, string leaf, string cmdlet, string deploymentName, string specId, string parameters) =>
            target.Run(
                WithSpecCli(target, leaf, deploymentName, specId, parameters),
                cmdlet,
                new(PowerShell(target, deploymentName))
                {
                    ["Location"] = Location,
                    ["TemplateSpecId"] = specId,
                    ["ParametersFile"] = parameters,
                });
    }

    // Runs a Bicep file with a parameter and an output through validate, what-if, and create,
    // then creates from a file whose nullable parameters are all omitted.
    private static class BicepCase
    {
        public const string DeploymentName = "scenario-sub-bicep";
        public const string NullableDeploymentName = "scenario-sub-bicep-nullable";
        private const string Greeting = "from-bicep";
        public const string Template =
            """
            targetScope = 'subscription'

            param greeting string

            output greeting string = greeting
            """;
        public const string NullableTemplate =
            """
            targetScope = 'subscription'

            type settings = {
              name: string
            }

            param yourName string?
            param config settings?

            output greeting string = 'Hello ${yourName ?? 'nobody'}'
            output configName string = config.?name ?? 'none'
            """;

        // Bicep stamps its version and a template hash into the compiled template, which would
        // break body matching whenever playback runs a different Bicep build than the recording.
        public static readonly IRecordingSanitizer GeneratorSanitizer =
            new RegexSanitizer("bicep-generator", "\"_generator\"\\s*:\\s*\\{[^{}]*\\}", "\"_generator\":{}");

        public sealed record Files(string Template, string NullableTemplate);

        public static void Lifecycle(Target target, Files files)
        {
            string name = target.Name(DeploymentName);
            DeployBicepFile(target, files, name);

            string nullableName = target.Name(NullableDeploymentName);
            DeployBicepFileWithNullableParameters(target, files, nullableName);

            Assert.Empty(Delete(target, name));
            Assert.Empty(Delete(target, nullableName));
        }

        // A subscription-scoped Bicep file compiles locally, then validates, runs what-if, and deploys.
        private static void DeployBicepFile(Target target, Files files, string name)
        {
            var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(
                WithBicep(target, "validate", "Test-AzDeployment", name, files.Template, $"greeting={Greeting}")));
            Assert.Null(validation.Error);
            Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);

            if (target.Surface == ClientType.CLI)
            {
                var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Host.RunCli(
                    WithBicepCli(target, "what-if", name, files.Template, $"greeting={Greeting}"))));
                Assert.Equal(State.Succeeded, whatIf.Status);
                Assert.Null(whatIf.Error);
                Assert.Empty(whatIf.Properties?.Changes ?? []);
            }

            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(
                WithBicep(target, "create", "New-AzDeployment", name, files.Template, $"greeting={Greeting}")));
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(Greeting, deployment.Properties?.Outputs?["greeting"].Value);
        }

        // Every parameter is nullable, so a create with none of them succeeds and the
        // outputs fall back to their defaults.
        private static void DeployBicepFileWithNullableParameters(Target target, Files files, string name)
        {
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(
                WithBicep(target, "create", "New-AzDeployment", name, files.NullableTemplate)));
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal("Hello nobody", deployment.Properties?.Outputs?["greeting"].Value);
            Assert.Equal("none", deployment.Properties?.Outputs?["configName"].Value);
        }

        public static void AssertRequestBodies(SessionRecord record)
        {
            var bodies = new[]
            {
                // The create validates too; the first validate POST is the standalone validate step.
                Requests(record, "POST", $"/deployments/{DeploymentName}/validate")[0],
                Assert.Single(Requests(record, "POST", $"/deployments/{DeploymentName}/whatIf")),
                Assert.Single(Requests(record, "PUT", $"/deployments/{DeploymentName}?")),
            };
            foreach (var body in bodies)
            {
                var properties = AssertCompiledTemplate(body);
                Assert.Equal(Greeting, properties.GetProperty("parameters").GetProperty("greeting").GetProperty("value").GetString());
            }

            var nullable = AssertCompiledTemplate(Assert.Single(Requests(record, "PUT", $"/deployments/{NullableDeploymentName}?")));
            if (nullable.TryGetProperty("parameters", out var parameters))
            {
                Assert.False(parameters.TryGetProperty("yourName", out _));
                Assert.False(parameters.TryGetProperty("config", out _));
            }
        }

        // The request carries the compiled ARM JSON inline, never the Bicep source or a link.
        private static JsonElement AssertCompiledTemplate(JsonElement body)
        {
            Assert.Equal(Location, body.GetProperty("location").GetString());
            var properties = body.GetProperty("properties");
            Assert.False(properties.TryGetProperty("templateLink", out _));
            Assert.Contains("subscriptionDeploymentTemplate", properties.GetProperty("template").GetProperty("$schema").GetString());
            return properties;
        }

        private static string[] WithBicepCli(Target target, string leaf, string deploymentName, string templateFile, params string[] parameters) =>
            Cli(target, leaf, deploymentName,
                ["--location", Location, "--template-file", templateFile, .. parameters.SelectMany(p => new[] { "--parameter", p })]);

        private static IReadOnlyList<object> WithBicep(Target target, string leaf, string cmdlet, string deploymentName, string templateFile, params string[] parameters)
        {
            var powerShell = new Dictionary<string, object>(PowerShell(target, deploymentName))
            {
                ["Location"] = Location,
                ["TemplateFile"] = templateFile,
            };
            if (parameters.Length > 0)
            {
                powerShell["Parameter"] = parameters;
            }

            return target.Run(WithBicepCli(target, leaf, deploymentName, templateFile, parameters), cmdlet, powerShell);
        }
    }

    // Looks for the Bicep CLI on PATH and in the az-bundled location, the same places the
    // extension's compiler looks.
    private static bool BicepCliInstalled()
    {
        string command = OperatingSystem.IsWindows() ? "bicep.exe" : "bicep";
        bool onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir.Trim(), command)));
        return onPath || File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure", "bin", command));
    }

    /// <summary>
    /// Trims the subscription-wide deployment list to the named deployments. The list otherwise
    /// returns every deployment in the recording subscription, including other people's names and
    /// outputs. The sanitizer changes only a JSON object with a <c>value</c> array, so request bodies,
    /// URIs, and single-deployment responses pass through unchanged.
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
