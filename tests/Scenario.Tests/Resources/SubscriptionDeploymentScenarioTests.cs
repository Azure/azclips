using System.Collections;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
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
/// <item>The review part covers inline values typed by the template, hashtable parameters, what-if
/// excluded change types, deployment tags, the debug log level, the resource provisioning progress
/// create writes to the verbose stream while it waits, CLI --no-wait create and delete, a
/// .bicepparam Parameter value, and the CLI option aliases.</item>
/// </list>
/// Record mode captures the CLI workflow against Azure. Playback replays the same traffic through
/// the CLI and PowerShell surfaces and matches request bodies, so both surfaces must bind their
/// arguments into the same recorded requests. PowerShell has no wait cmdlet at subscription scope,
/// so wait runs on the CLI only; the lifecycle's export runs on PowerShell as the real
/// Save-AzDeploymentTemplate cmdlet fed the piped deployment, which sends the same request and
/// writes the template to a file. On PowerShell, create and get write the Az.Resources-shaped
/// PSDeployment, while the CLI writes the ARM DeploymentExtended.
/// On PowerShell the lifecycle's delete, in-flight cancel, and in-flight delete run the real
/// cmdlets by -Id, piped PSDeployment, and -InputObject, with and without -PassThru; they send the
/// same requests as by name.
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
        // The .bicepparam file names its Bicep file by a path relative to itself; both live in the temp directory.
        string reviewBicep = files.Write(BicepCase.Template, ".bicep");
        var review = new ReviewCase.Files(
            files.Write(ReviewCase.TypedTemplate), files.Write(ReviewCase.TwoGroupTemplate), template,
            files.Write(ReviewCase.BicepParameters(Path.GetFileName(reviewBicep)), ".bicepparam"),
            files.Write(ReviewCase.ProgressTemplate));

        RunScenario(
            WithDeploymentList(DeploymentName, SlowDeploymentName, ParametersCase.DeploymentName).With([BicepCase.GeneratorSanitizer]),
            target =>
            {
                Lifecycle(target, template, invalidTemplate, failingTemplate);
                ParametersCase.Lifecycle(target, parameters);
                WhatIfCase.Lifecycle(target, whatIf);
                TemplateSpecCase.Lifecycle(target, specParameters);
                // The extension compiles Bicep locally before it sends the request, so playback
                // skips these steps on a machine without the Bicep CLI.
                if (target.Live || BicepCliInstalled())
                {
                    BicepCase.Lifecycle(target, bicep);
                }
                ReviewCase.Lifecycle(target, review, specParameters);
            },
            record =>
            {
                AssertRequestBodies(record);
                ParametersCase.AssertRequestBodies(record);
                WhatIfCase.AssertRequestBodies(record);
                TemplateSpecCase.AssertRequestBodies(record);
                BicepCase.AssertRequestBodies(record);
                ReviewCase.AssertRequestBodies(record);
            },
            target =>
            {
                foreach (var name in new[]
                {
                    DeploymentName, SlowDeploymentName, FailedDeploymentName, CreateFailedDeploymentName,
                    ParametersCase.DeploymentName, WhatIfCase.SetupDeploymentName, TemplateSpecCase.DeploymentName,
                    BicepCase.DeploymentName, BicepCase.NullableDeploymentName,
                    ReviewCase.TypedName, ReviewCase.TaggedName, ReviewCase.NoWaitName, ReviewCase.ProgressName,
                })
                {
                    Quietly(() => Delete(target, target.Name(name)));
                }
                Quietly(() => DeleteGroup(target, SlowResourceGroupName));
                Quietly(() => DeleteGroup(target, WhatIfCase.GroupName));
                Quietly(() => DeleteGroup(target, ReviewCase.ProgressGroupName));
                // What-if and validate never create these groups; the deletes only guard against a stray deployment.
                Quietly(() => DeleteGroup(target, ReviewCase.NewGroupName));
                Quietly(() => DeleteGroup(target, ReviewCase.UriGroupName));
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

        var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(WithTemplate(target, "what-if", "Get-AzDeploymentWhatIfResult", name, template)));
        Assert.Equal(State.Succeeded, whatIf.Status);
        Assert.Null(whatIf.Error);
        Assert.Empty(whatIf.Properties?.Changes ?? []);

        AssertSucceeded(target, WithTemplate(target, "create", "New-AzDeployment", name, template));

        if (target.Surface == ClientType.CLI)
        {
            Assert.Empty(target.Host.RunCli(Cli(target, "wait", name, "--exists", "--deleted", "--interval", "1")));
        }

        var shown = Show(target, name);
        AssertSucceeded(target, shown);

        Assert.Contains(name, ListNames(target));

        if (target.Surface == ClientType.CLI)
        {
            AssertExportedTemplate(Assert.IsType<JsonElement>(Assert.Single(target.Host.RunCli(Cli(target, "export", name)))));
        }
        else
        {
            // Same export POST as the CLI; the piped PSDeployment binds -Name by its DeploymentName
            // property, and the cmdlet writes the template to the file and outputs its path.
            string path = Path.Combine(Path.GetTempPath(), $"azclips-sub-export-{Guid.NewGuid():N}.json");
            try
            {
                var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(target.Host.RunPowerShellCmdlet<SaveDeploymentTemplateCmdlet>(
                    new Dictionary<string, object>
                    {
                        ["SubscriptionId"] = target.SubscriptionId,
                        ["Path"] = path,
                        ["Force"] = true,
                    },
                    pipelineInput: Assert.Single(shown))));
                Assert.Equal(path, saved.Path);

                using var document = JsonDocument.Parse(File.ReadAllText(path));
                AssertExportedTemplate(document.RootElement);
            }
            finally
            {
                File.Delete(path);
            }
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
            Assert.Empty(target.Host.RunCli(Cli(target, "wait", name, "--updated", "--deleted", "--interval", "1")));
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

        var noCondition = Assert.Throws<InvalidInputException>(() => target.Host.RunCli(Cli(target, "wait", name)));
        Assert.Contains("Specify at least one of --created, --updated, --deleted, or --exists.", noCondition.Message);

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

    // The wait lists the operations before each status poll; the failure message lists them once
    // more after the deployment GET.
    private static void AssertFailureFollowUpRequests(SessionRecord record)
    {
        string deployment = $"/deployments/{CreateFailedDeploymentName}";
        Assert.Single(record.Entries, e => IsRequest(e, "GET", $"{deployment}?"));
        int get = record.Entries.FindIndex(e => IsRequest(e, "GET", $"{deployment}?"));
        int operations = record.Entries.FindLastIndex(e => IsRequest(e, "GET", $"{deployment}/operations?"));
        Assert.True(operations > get, "The failure message didn't list the operations after the deployment GET.");
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
        object output = Assert.Single(outputs);
        var deployment = AsDeployment(target, output);
        Assert.Equal(target.Name(DeploymentName), deployment.Name);
        Assert.Equal(State.Succeeded, deployment.ProvisioningState);
        Assert.Equal(Greeting, deployment.Outputs?["greeting"].Value);
        Assert.Equal(Greeting, deployment.Parameters?["greeting"].Value);

        if (output is PSDeployment ps)
        {
            // The Az.Resources properties, flattened from the ARM deployment.
            Assert.Equal(DeploymentId(target, target.Name(DeploymentName)), ps.Id, ignoreCase: true);
            Assert.Equal(Location, ps.Location);
            Assert.Equal(DeploymentMode.Incremental, ps.Mode);
            Assert.NotEqual(default, ps.Timestamp);
            Assert.False(string.IsNullOrEmpty(ps.CorrelationId));
            Assert.Empty(ps.Tags);
            Assert.Null(ps.TemplateLink);
            Assert.Null(ps.TemplateLinkString);
            Assert.Null(ps.DeploymentDebugLogLevel);
            AssertVariableTable(ps.ParametersString, "greeting", "String", $"\"{Greeting}\"");
            AssertVariableTable(ps.OutputsString, "greeting", "String", $"\"{Greeting}\"");
        }
    }

    // The common view of a written deployment. PowerShell writes the Az.Resources-shaped PSDeployment
    // and the CLI writes the raw ARM DeploymentExtended.
    private sealed record DeploymentView(
        string? Id,
        string? Name,
        string? ProvisioningState,
        Dictionary<string, DeploymentVariable>? Parameters,
        Dictionary<string, DeploymentVariable>? Outputs,
        TemplateLink? TemplateLink,
        IDictionary<string, string>? Tags,
        string? DebugLevel);

    private static DeploymentView AsDeployment(Target target, object? output)
    {
        if (target.Surface == ClientType.PowerShell)
        {
            var ps = Assert.IsType<PSDeployment>(output);
            Assert.Null(ps.ResourceGroupName);
            Assert.Null(ps.ManagementGroupId);
            return new(ps.Id, ps.DeploymentName, ps.ProvisioningState, ps.Parameters, ps.Outputs, ps.TemplateLink, ps.Tags, ps.DeploymentDebugLogLevel);
        }

        var raw = Assert.IsType<DeploymentExtended>(output);
        return new(raw.Id, raw.Name, raw.Properties?.ProvisioningState, raw.Properties?.Parameters, raw.Properties?.Outputs,
            raw.Properties?.TemplateLink, raw.Tags, raw.Properties?.DebugSetting?.DetailLevel);
    }

    // PSDeployment renders parameters and outputs as Az.Resources' Name/Type/Value table, with JSON values.
    private static void AssertVariableTable(string? table, string name, string type, string value)
    {
        Assert.NotNull(table);
        var lines = table.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        Assert.Matches(@"^Name\s+Type\s+Value\s*$", lines[1]);
        Assert.Matches(@"^=+\s+=+\s+=+\s*$", lines[2]);
        Assert.Contains(lines, line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var n, var t, ..] && n == name && t == type
            && line.TrimEnd().EndsWith(value, StringComparison.Ordinal));
    }

    // The list command writes every page as one collected list: List<DeploymentExtended> on the
    // CLI and List<PSDeployment> on PowerShell.
    private static List<string?> ListNames(Target target)
    {
        object output = Assert.Single(target.Run(
            ["deployment", "sub", "list", "--subscription-id", target.SubscriptionId],
            "Get-AzDeployment_List",
            new() { ["SubscriptionId"] = target.SubscriptionId }));
        return target.Surface == ClientType.PowerShell
            ? [.. Assert.IsType<List<PSDeployment>>(output).Select(d => d.DeploymentName)]
            : [.. Assert.IsType<List<DeploymentExtended>>(output).Select(d => d.Name)];
    }

    // Returns the canceled deployment as the surface wrote it.
    private static object CancelInFlightDeployment(Target target)
    {
        // `deployment sub create` blocks until the deployment is terminal, so start the
        // deployment to cancel with a raw PUT that returns as soon as ARM accepts it.
        var response = Assert.IsType<RestResponse>(Assert.Single(StartSlowDeployment(target)));
        Assert.Equal(201, response.StatusCode);

        var inFlight = WaitForState(target, attempts: 15, State.Accepted, State.Running);
        string? state = AsDeployment(target, inFlight).ProvisioningState;
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
        Assert.Equal(State.Canceled, AsDeployment(target, canceled).ProvisioningState);
        return canceled;
    }

    private static void AssertCancelFails(string expectedCode, Target target, string deploymentName)
    {
        var error = Assert.Throws<HttpRequestException>(() => Cancel(target, deploymentName));
        Assert.Contains(expectedCode, error.Message);
    }

    // Stops at a wanted state or any terminal one, and returns the deployment as the surface wrote it.
    // Playback replays the recorded responses in order, so it takes the same number of polls; only
    // record mode waits between them.
    private static object WaitForState(Target target, int attempts, params string[] wanted)
    {
        object? deployment = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            deployment = Assert.Single(Show(target, target.Name(SlowDeploymentName)));
            string? state = AsDeployment(target, deployment).ProvisioningState;
            if (wanted.Contains(state) || state is State.Succeeded or State.Failed or State.Canceled)
            {
                break;
            }

            if (target.Live)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
        Assert.NotNull(deployment);
        return deployment;
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

    private static void AssertExportedTemplate(JsonElement template)
    {
        Assert.Contains("subscriptionDeploymentTemplate", template.GetProperty("$schema").GetString());
        Assert.True(template.GetProperty("outputs").TryGetProperty("greeting", out _), $"Exported template has no greeting output: {template}");
    }

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
    // list filter, so that step runs on the CLI only.
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

            WhatIfWithValidationLevel(target, files, name);

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

        // Checks that what-if sends the ProviderNoRbac validation level.
        private static void WhatIfWithValidationLevel(Target target, Files files, string name)
        {
            var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Run(
                Cli(target, "what-if", name, "--location", Location, "--template-file", files.Template,
                    "--parameters-file", files.Parameters, "--parameter", $"greeting={OverlayGreeting}", "--validation-level", "ProviderNoRbac"),
                "Get-AzDeploymentWhatIfResult",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.Template,
                    ["ParametersFile"] = files.Parameters,
                    ["Parameter"] = new[] { $"greeting={OverlayGreeting}" },
                    ["ValidationLevel"] = "ProviderNoRbac",
                })));
            Assert.Equal(State.Succeeded, whatIf.Status);
            Assert.Null(whatIf.Error);
            Assert.Empty(whatIf.Properties?.Changes ?? []);
        }

        // Creates with the overlaid parameters file at the Provider validation level, and checks
        // that the outputs carry the merged values.
        private static void CreateWithParametersFile(Target target, Files files, string name)
        {
            var deployment = AsDeployment(target, Assert.Single(target.Run(
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
            var deployment = AsDeployment(target, Assert.Single(target.Run(
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

        private static void AssertMergedOutputs(string name, DeploymentView deployment)
        {
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(OverlayGreeting, deployment.Outputs?["greeting"].Value);
            Assert.Equal(FileGroupName, deployment.Outputs?["nestedRGName"].Value);
            Assert.Equal(OverlayGreeting, deployment.Parameters?["greeting"].Value);
            Assert.Equal(FileGroupName, deployment.Parameters?["nestedRGName"].Value);
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
    // creates a tagged resource group that the what-if calls compare against.
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
            var setup = AsDeployment(target, Assert.Single(TaggedGroup(
                target, "create", "New-AzDeployment", target.Name(SetupDeploymentName), files, group, "one")));
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
            var result = WhatIf(TaggedGroup(target, "what-if", "Get-AzDeploymentWhatIfResult", target.Name(ModifyName), files, group, "two"));
            var change = Assert.Single(result.Properties?.Changes ?? []);
            Assert.Equal("Modify", change.ChangeType);
            Assert.EndsWith($"/resourceGroups/{group}", change.ResourceId, StringComparison.OrdinalIgnoreCase);
            AssertTagDelta(change, "one", "two");
        }

        // What-if reports the conditional group as a potential change, the group whose name
        // it can't calculate as unsupported, and the nested deployment as short-circuited.
        private static void WhatIfShortCircuit(Target target, Files files, string group)
        {
            string name = target.Name(ShortCircuitName);
            var result = WhatIf(target.Run(
                Cli(target, "what-if", name, "--location", Location, "--template-file", files.ShortCircuit,
                    "--parameter", $"groupName={group}"),
                "Get-AzDeploymentWhatIfResult",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.ShortCircuit,
                    ["Parameter"] = new[] { $"groupName={group}" },
                }));

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

            var full = Assert.Single(WhatIf(TaggedGroup(target, "what-if", "Get-AzDeploymentWhatIfResult", target.Name(CreateName), files, group, "one")).Properties?.Changes ?? []);
            Assert.Equal("Create", full.ChangeType);
            Assert.EndsWith($"/resourceGroups/{group}", full.ResourceId, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(full.After);
            Assert.Null(full.Before);

            var idOnly = Assert.Single(WhatIf(TaggedGroup(
                target, "what-if", "Get-AzDeploymentWhatIfResult", target.Name(ResourceIdOnlyName), files, group, "one", "ResourceIdOnly")).Properties?.Changes ?? []);
            Assert.Equal("Create", idOnly.ChangeType);
            Assert.EndsWith($"/resourceGroups/{group}", idOnly.ResourceId, StringComparison.OrdinalIgnoreCase);
            Assert.Null(idOnly.Before);
            Assert.Null(idOnly.After);
            Assert.Null(idOnly.Delta);
        }

        private static WhatIfOperationResult WhatIf(IReadOnlyList<object> output)
        {
            var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output));
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

        // The result format, when given, is passed to the what-if call only.
        private static IReadOnlyList<object> TaggedGroup(
            Target target, string leaf, string cmdlet, string deploymentName, Files files, string group, string tagValue, string? resultFormat = null)
        {
            string[] cli = Cli(target, leaf, deploymentName, "--location", Location, "--template-file", files.TaggedGroup,
                "--parameter", $"groupName={group}", "--parameter", $"tagValue={tagValue}");
            var ps = new Dictionary<string, object>(PowerShell(target, deploymentName))
            {
                ["Location"] = Location,
                ["TemplateFile"] = files.TaggedGroup,
                ["Parameter"] = new[] { $"groupName={group}", $"tagValue={tagValue}" },
            };
            if (resultFormat is not null)
            {
                cli = [.. cli, "--result-format", resultFormat];
                ps["ResultFormat"] = resultFormat;
            }
            return target.Run(cli, cmdlet, ps);
        }
    }

    // Validates, runs what-if on, creates, shows, and exports a deployment whose
    // template comes from a template spec version. A final create references a version that
    // doesn't exist. The setup creates a resource group and the spec with raw PUTs.
    private static class TemplateSpecCase
    {
        public const string DeploymentName = "scenario-sub-ts-deployment";
        private const string MissingVersionDeploymentName = "scenario-sub-ts-missing-version";
        public const string GroupName = "scenario-sub-ts-rg";
        public const string SpecName = "scenario-sub-ts-spec";
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
            AssertDeployedFromSpec(target, name, specId, WithSpec(target, "create", "New-AzDeployment", name, specId, parameters));
            AssertDeployedFromSpec(target, name, specId, Show(target, name));

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

        public static string SpecId(Target target, string version) =>
            $"/subscriptions/{target.SubscriptionId}/resourceGroups/{target.Name(GroupName)}/providers/Microsoft.Resources/templateSpecs/{SpecName}/versions/{version}";

        private static void AssertDeployedFromSpec(Target target, string name, string specId, IReadOnlyList<object> outputs)
        {
            object output = Assert.Single(outputs);
            var deployment = AsDeployment(target, output);
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(Greeting, deployment.Outputs?["greeting"].Value);
            Assert.Equal(specId, deployment.TemplateLink?.Id, ignoreCase: true);
            if (output is PSDeployment ps)
            {
                // Az.Resources renders only the link's Uri and ContentVersion; ARM returns a spec link without a Uri.
                string nl = Environment.NewLine;
                Assert.Equal($"{nl}Uri            : {nl}ContentVersion : 1.0.0.0{nl}", ps.TemplateLinkString);
            }
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

            var deployment = AsDeployment(target, Assert.Single(
                WithBicep(target, "create", "New-AzDeployment", name, files.Template, $"greeting={Greeting}")));
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(Greeting, deployment.Outputs?["greeting"].Value);
        }

        // Every parameter is nullable, so a create with none of them succeeds and the
        // outputs fall back to their defaults.
        private static void DeployBicepFileWithNullableParameters(Target target, Files files, string name)
        {
            var deployment = AsDeployment(target, Assert.Single(
                WithBicep(target, "create", "New-AzDeployment", name, files.NullableTemplate)));
            Assert.Equal(name, deployment.Name);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal("Hello nobody", deployment.Outputs?["greeting"].Value);
            Assert.Equal("none", deployment.Outputs?["configName"].Value);
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

    // Covers the review options: inline values typed by the template, hashtable parameters, what-if
    // excluded change types, deployment tags and debug level, create's provisioning progress, CLI
    // --no-wait, a .bicepparam Parameter value, and the CLI option aliases. Steps that only one
    // surface has run on that surface.
    private static class ReviewCase
    {
        public const string TypedName = "scenario-sub-review-typed";
        private const string ExcludeName = "scenario-sub-review-exclude";
        public const string TaggedName = "scenario-sub-review-tagged";
        public const string NoWaitName = "scenario-sub-review-nowait";
        public const string ProgressName = "scenario-sub-review-progress";
        private const string BicepParamName = "scenario-sub-review-bicepparam";
        private const string BicepParamFileName = "scenario-sub-review-bicepparam-file";
        private const string UriName = "scenario-sub-review-uri";
        private const string SpecShortName = "scenario-sub-review-spec-s";
        private const string SpecLongName = "scenario-sub-review-spec-long";
        public const string NewGroupName = "scenario-sub-review-new-rg";
        public const string UriGroupName = "scenario-sub-review-uri-rg";
        public const string ProgressGroupName = "scenario-sub-review-progress-rg";
        private const string BicepParamGreeting = "from-bicepparam";
        private const string DebugDetailLevel = "RequestContent, ResponseContent";
        private const string ApiVersion = "2025-04-01";
        // The deployment operations list uses a newer API version than the deployment itself.
        private const string OperationsApiVersion = "2026-06-01";
        // The URI pins a commit, so the template ARM fetches never changes between re-records.
        private const string TemplateUri =
            "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/8295dc7cbbacd776765e79091f0558300d4c3d67/subscription-deployments/create-rg/azuredeploy.json";
        public const string TypedTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"label":{"type":"string"},"count":{"type":"int"},"enabled":{"type":"bool"},"settings":{"type":"object"},"zones":{"type":"array"},"raw":{"type":"string"}},"resources":[],"outputs":{"label":{"type":"string","value":"[parameters('label')]"},"count":{"type":"int","value":"[parameters('count')]"},"enabled":{"type":"bool","value":"[parameters('enabled')]"},"settings":{"type":"object","value":"[parameters('settings')]"},"zones":{"type":"array","value":"[parameters('zones')]"},"raw":{"type":"string","value":"[parameters('raw')]"}}}""";
        // The existing group matches what WhatIfCase's setup deployment left behind, so what-if
        // reports it as NoChange; the new group doesn't exist, so it's a Create.
        public const string TwoGroupTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"existingGroup":{"type":"string"},"newGroup":{"type":"string"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('existingGroup')]","location":"westus2","tags":{"a":"one"}},{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('newGroup')]","location":"westus2","tags":{"a":"one"}}]}""";
        // One cheap resource, so the deployment has an operation with a target resource to report.
        public const string ProgressTemplate =
            """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"groupName":{"type":"string"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('groupName')]","location":"westus2"}]}""";
        // The inline tokens after label; on PowerShell label comes from -TemplateParameterObject.
        private static readonly string[] s_typedTokens = ["count=3", "enabled=true", """settings={"tier":"basic"}""", "zones=[1,2]", """raw={"a":1}"""];
        // The processors serialize request bodies with these options.
        private static readonly JsonSerializerOptions s_armJson = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public sealed record Files(string Typed, string TwoGroups, string Greeting, string BicepParam, string Progress);

        public static string BicepParameters(string bicepFileName) =>
            $"""
            using './{bicepFileName}'

            param greeting = '{BicepParamGreeting}'
            """;

        public static void Lifecycle(Target target, Files files, string specParameters)
        {
            TypedParameters(target, files);
            ExcludeChangeTypes(target, files);
            TagsAndDebugLevel(target, files);
            Progress(target, files);
            if (target.Surface == ClientType.CLI)
            {
                NoWait(target, files);
            }
            // The extension compiles the .bicepparam file locally, like BicepCase.
            if (target.Live || BicepCliInstalled())
            {
                BicepParamAsParameter(target, files);
            }
            Aliases(target, specParameters);
        }

        // T1 and T2: CLI sends every value inline; PowerShell sends label in the hash table and
        // the rest inline. Both build the same bodies, typed by the template's declared types.
        private static void TypedParameters(Target target, Files files)
        {
            string name = target.Name(TypedName);
            IReadOnlyList<object> Run(string leaf, string cmdlet) => target.Run(
                Sub(target, leaf, ["-n", name, "-l", Location, "-f", files.Typed, "--parameters", $"label={Greeting}", .. s_typedTokens]),
                cmdlet,
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.Typed,
                    ["TemplateParameterObject"] = new Hashtable { ["label"] = Greeting },
                    ["Parameter"] = s_typedTokens,
                });

            AssertValid(Run("validate", "Test-AzDeployment"));

            var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(Run("what-if", "Get-AzDeploymentWhatIfResult")));
            Assert.Equal(State.Succeeded, whatIf.Status);
            Assert.Null(whatIf.Error);
            Assert.Empty(whatIf.Properties?.Changes ?? []);

            object created = Assert.Single(Run("create", "New-AzDeployment"));
            var deployment = AsDeployment(target, created);
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            var outputs = Assert.IsType<Dictionary<string, DeploymentVariable>>(deployment.Outputs);
            Assert.Equal(Greeting, outputs["label"].Value);
            Assert.Equal(3L, outputs["count"].Value);
            Assert.Equal(true, outputs["enabled"].Value);
            var settings = Assert.IsType<Dictionary<string, object?>>(outputs["settings"].Value);
            Assert.Equal("basic", Assert.Single(settings, s => s.Key == "tier").Value);
            Assert.Equal(new object?[] { 1L, 2L }, Assert.IsType<List<object?>>(outputs["zones"].Value));
            Assert.Equal("""{"a":1}""", outputs["raw"].Value);
            if (created is PSDeployment ps)
            {
                // Each value renders as JSON under the type ARM reports.
                AssertVariableTable(ps.OutputsString, "label", "String", $"\"{Greeting}\"");
                AssertVariableTable(ps.OutputsString, "count", "Int", "3");
                AssertVariableTable(ps.OutputsString, "enabled", "Bool", "true");
                AssertVariableTable(ps.OutputsString, "settings", "Object", """{"tier":"basic"}""");
                AssertVariableTable(ps.OutputsString, "zones", "Array", "[1,2]");
                AssertVariableTable(ps.OutputsString, "raw", "String", """
                    "{\"a\":1}"
                    """);
                AssertVariableTable(ps.ParametersString, "zones", "Array", "[1,2]");
            }

            Assert.Empty(Delete(target, name));
        }

        // T3 on the CLI with -x, T4 on PowerShell with -ExcludeChangeType. The existing group's
        // NoChange is dropped and only the new group's Create is left.
        private static void ExcludeChangeTypes(Target target, Files files)
        {
            string name = target.Name(ExcludeName);
            string existing = target.Name(WhatIfCase.GroupName);
            string created = target.Name(NewGroupName);
            var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(target.Run(
                Sub(target, "what-if", "-n", name, "-l", Location, "-f", files.TwoGroups,
                    "-p", $"existingGroup={existing}", $"newGroup={created}", "-x", "NoChange"),
                "Get-AzDeploymentWhatIfResult",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.TwoGroups,
                    ["Parameter"] = new[] { $"existingGroup={existing}", $"newGroup={created}" },
                    ["ExcludeChangeType"] = new[] { "NoChange" },
                })));
            Assert.Equal(State.Succeeded, result.Status);
            Assert.Null(result.Error);
            var change = Assert.Single(result.Properties?.Changes ?? []);
            Assert.Equal("Create", change.ChangeType);
            Assert.EndsWith($"/resourceGroups/{created}", change.ResourceId, StringComparison.OrdinalIgnoreCase);
        }

        // T5 and T6. The CLI has no tags or debug level option, so the record run sends the bodies
        // the PowerShell cmdlets send through rest: the validate, the create's validate, the PUT,
        // its polls each after an operations list, and a GET. Playback then runs the real cmdlets
        // against those requests.
        private static void TagsAndDebugLevel(Target target, Files files)
        {
            string name = target.Name(TaggedName);
            DeploymentView deployment;
            if (target.Surface == ClientType.CLI)
            {
                string uri = $"https://management.azure.com/subscriptions/{target.SubscriptionId}/providers/Microsoft.Resources/deployments/{name}";
                AssertValidRest(Rest(target, "POST", $"{uri}/validate?api-version={ApiVersion}", TaggedBody(files.Greeting, debug: false)));

                string body = TaggedBody(files.Greeting, debug: true);
                AssertValidRest(Rest(target, "POST", $"{uri}/validate?api-version={ApiVersion}", body));
                var put = Rest(target, "PUT", $"{uri}?api-version={ApiVersion}", body);
                Assert.True(put.StatusCode is 200 or 201, $"PUT returned {put.StatusCode}.");
                string status = put.Headers["Azure-AsyncOperation"];
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    Rest(target, "GET", $"{uri}/operations?api-version={OperationsApiVersion}");
                    string? state = Rest(target, "GET", status).Json?.GetProperty("status").GetString();
                    if (state is State.Succeeded or State.Failed or State.Canceled)
                    {
                        break;
                    }
                    if (target.Live)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                }
                deployment = AsDeployment(target, Rest(target, "GET", $"{uri}?api-version={ApiVersion}").Json?.Deserialize<DeploymentExtended>(s_armJson));
            }
            else
            {
                Dictionary<string, object> Tagged() => new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.Greeting,
                    ["Parameter"] = new[] { $"greeting={Greeting}" },
                    ["Tag"] = new Dictionary<string, string> { ["env"] = "review" },
                };
                AssertValid(target.Host.RunPowerShell("Test-AzDeployment", Tagged()));
                var create = Tagged();
                create["DeploymentDebugLogLevel"] = "All";
                deployment = AsDeployment(target, Assert.Single(target.Host.RunPowerShell("New-AzDeployment", create)));
            }

            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal("review", Assert.Single(deployment.Tags ?? new Dictionary<string, string>(), t => t.Key == "env").Value);
            Assert.Equal(DebugDetailLevel, deployment.DebugLevel);
            Assert.Equal(Greeting, deployment.Outputs?["greeting"].Value);

            Assert.Empty(Delete(target, name));
        }

        // F24: while create waits, it lists the deployment operations before each status poll and
        // writes each new resource provisioning state, lowercased like Azure PowerShell, to the
        // verbose stream. The output is still just the deployment; the CLI writes the verbose
        // stream to stderr, and only with --debug.
        private static void Progress(Target target, Files files)
        {
            string name = target.Name(ProgressName);
            string group = target.Name(ProgressGroupName);
            var deployment = AsDeployment(target, Assert.Single(target.Run(
                Sub(target, "create", "-n", name, "-l", Location, "-f", files.Progress, "-p", $"groupName={group}"),
                "New-AzDeployment",
                new(PowerShell(target, name))
                {
                    ["Location"] = Location,
                    ["TemplateFile"] = files.Progress,
                    ["Parameter"] = new[] { $"groupName={group}" },
                })));
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);

            // How many polls see the group's operation, and in which state, depends on ARM's timing.
            string prefix = $"Resource Microsoft.Resources/resourceGroups '{group}' provisioning status is ";
            var states = target.Host.Verbose.Where(m => m.StartsWith(prefix, StringComparison.Ordinal)).Select(m => m[prefix.Length..]).ToList();
            Assert.NotEmpty(states);
            Assert.All(states, state => Assert.Equal(state.ToLowerInvariant(), state));
            Assert.Equal(states.Distinct().Count(), states.Count);

            Assert.Empty(Delete(target, name));
        }

        // T7, CLI only: create and delete with --no-wait print nothing; wait and show confirm the result.
        private static void NoWait(Target target, Files files)
        {
            string name = target.Name(NoWaitName);
            Assert.Empty(target.Host.RunCli(Sub(target, "create", "-n", name, "-l", Location, "-f", files.Greeting, "-p", $"greeting={Greeting}", "--no-wait")));
            Assert.Empty(target.Host.RunCli(Sub(target, "wait", "-n", name, "--created", "--interval", "1")));

            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(target.Host.RunCli(Sub(target, "show", "-n", name))));
            Assert.Equal(State.Succeeded, deployment.ProvisioningState);
            Assert.Equal(Greeting, deployment.Properties?.Outputs?["greeting"].Value);

            Assert.Empty(target.Host.RunCli(Sub(target, "delete", "-n", name, "--no-wait")));
            Assert.Empty(target.Host.RunCli(Sub(target, "wait", "-n", name, "--deleted", "--interval", "1")));
        }

        // T8: a .bicepparam path given as a Parameter value validates like the parameters-file option.
        private static void BicepParamAsParameter(Target target, Files files)
        {
            string name = target.Name(BicepParamName);
            AssertValid(target.Run(
                Sub(target, "validate", "-n", name, "-l", Location, "-p", files.BicepParam),
                "Test-AzDeployment",
                new(PowerShell(target, name)) { ["Location"] = Location, ["Parameter"] = new[] { files.BicepParam } }));

            string fileName = target.Name(BicepParamFileName);
            AssertValid(target.Run(
                Sub(target, "validate", "-n", fileName, "-l", Location, "--parameters-file", files.BicepParam),
                "Test-AzDeployment",
                new(PowerShell(target, fileName)) { ["Location"] = Location, ["ParametersFile"] = files.BicepParam }));
        }

        // T9: -u and -p on the CLI, -TemplateUri on PowerShell; -s and --template-spec on the CLI only.
        private static void Aliases(Target target, string specParameters)
        {
            string uriName = target.Name(UriName);
            string group = target.Name(UriGroupName);
            AssertValid(target.Run(
                Sub(target, "validate", "-n", uriName, "-l", Location, "-u", TemplateUri, "-p", $"rgName={group}"),
                "Test-AzDeployment",
                new(PowerShell(target, uriName))
                {
                    ["Location"] = Location,
                    ["TemplateUri"] = TemplateUri,
                    ["Parameter"] = new[] { $"rgName={group}" },
                }));

            if (target.Surface == ClientType.CLI)
            {
                string specId = TemplateSpecCase.SpecId(target, "v1");
                AssertValid(target.Host.RunCli(Sub(target, "validate",
                    "-n", target.Name(SpecShortName), "-l", Location, "-s", specId, "--parameters-file", specParameters)));
                AssertValid(target.Host.RunCli(Sub(target, "validate",
                    "-n", target.Name(SpecLongName), "-l", Location, "--template-spec", specId, "--parameters-file", specParameters)));
            }
        }

        public static void AssertRequestBodies(SessionRecord record)
        {
            // T1 and T2: the standalone validate, the create's validate, the what-if, and the PUT.
            var typedValidates = Requests(record, "POST", $"/deployments/{TypedName}/validate");
            Assert.Equal(2, typedValidates.Count);
            foreach (var body in typedValidates
                .Append(Assert.Single(Requests(record, "POST", $"/deployments/{TypedName}/whatIf")))
                .Append(Assert.Single(Requests(record, "PUT", $"/deployments/{TypedName}?"))))
            {
                var parameters = body.GetProperty("properties").GetProperty("parameters");
                var label = parameters.EnumerateObject().First();
                Assert.Equal("label", label.Name);
                Assert.Equal(Greeting, label.Value.GetProperty("value").GetString());
                Assert.Equal(JsonValueKind.Number, Value(parameters, "count").ValueKind);
                Assert.Equal(JsonValueKind.True, Value(parameters, "enabled").ValueKind);
                Assert.Equal(JsonValueKind.Object, Value(parameters, "settings").ValueKind);
                Assert.Equal(JsonValueKind.Array, Value(parameters, "zones").ValueKind);
                Assert.Equal("""{"a":1}""", Value(parameters, "raw").GetString());
            }

            // T3: the service still reports the existing group; only the client drops it.
            int whatIf = record.Entries.ToList().FindIndex(e => IsRequest(e, "POST", $"/deployments/{ExcludeName}/whatIf"));
            Assert.True(whatIf >= 0, "The recording has no what-if POST for the exclude case.");
            var resultEntry = record.Entries.Skip(whatIf).First(e => e.Response.Body?.Contains("\"changes\"", StringComparison.Ordinal) == true);
            using (var result = JsonDocument.Parse(resultEntry.Response.Body!))
            {
                var changes = result.RootElement.GetProperty("properties").GetProperty("changes").EnumerateArray().ToList();
                Assert.Contains(changes, c => c.GetProperty("changeType").GetString() == "NoChange"
                    && c.GetProperty("resourceId").GetString()!.EndsWith($"/resourceGroups/{WhatIfCase.GroupName}", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(changes, c => c.GetProperty("changeType").GetString() == "Create"
                    && c.GetProperty("resourceId").GetString()!.EndsWith($"/resourceGroups/{NewGroupName}", StringComparison.OrdinalIgnoreCase));
            }

            // T5 and T6: every body carries the tags; only the create's validate and PUT carry the debug level.
            var taggedValidates = Requests(record, "POST", $"/deployments/{TaggedName}/validate");
            Assert.Equal(2, taggedValidates.Count);
            var taggedPut = Assert.Single(Requests(record, "PUT", $"/deployments/{TaggedName}?"));
            foreach (var body in taggedValidates.Append(taggedPut))
            {
                Assert.Equal("review", body.GetProperty("tags").GetProperty("env").GetString());
            }
            Assert.False(taggedValidates[0].GetProperty("properties").TryGetProperty("debugSetting", out _));
            foreach (var body in new[] { taggedValidates[1], taggedPut })
            {
                Assert.Equal(DebugDetailLevel, body.GetProperty("properties").GetProperty("debugSetting").GetProperty("detailLevel").GetString());
            }

            // T7: the --no-wait create sends the PUT but never polls it or lists its operations.
            Assert.Single(Requests(record, "PUT", $"/deployments/{NoWaitName}?"));
            Assert.DoesNotContain(record.Entries, e => e.Request.Method == "GET"
                && e.Request.Uri.Contains($"/deployments/{NoWaitName}/operationStatuses", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(record.Entries, e => IsRequest(e, "GET", $"/deployments/{NoWaitName}/operations?"));

            // F24: the create lists the deployment operations while it waits.
            Assert.Contains(record.Entries, e => IsRequest(e, "GET", $"/deployments/{ProgressName}/operations?api-version={OperationsApiVersion}"));

            // T8: the Parameter value and the parameters-file option send the same properties.
            var fromParameter = Assert.Single(Requests(record, "POST", $"/deployments/{BicepParamName}/validate")).GetProperty("properties");
            var fromFile = Assert.Single(Requests(record, "POST", $"/deployments/{BicepParamFileName}/validate")).GetProperty("properties");
            Assert.Equal(fromFile.GetRawText(), fromParameter.GetRawText());
            Assert.Equal(BicepParamGreeting, Value(fromParameter.GetProperty("parameters"), "greeting").GetString());
            Assert.Contains("subscriptionDeploymentTemplate", fromParameter.GetProperty("template").GetProperty("$schema").GetString());

            // T9: the URI and template spec aliases bind to the template link.
            var uri = Assert.Single(Requests(record, "POST", $"/deployments/{UriName}/validate")).GetProperty("properties");
            Assert.Equal(TemplateUri, uri.GetProperty("templateLink").GetProperty("uri").GetString());
            Assert.Equal(UriGroupName, Value(uri.GetProperty("parameters"), "rgName").GetString());
            foreach (var specName in new[] { SpecShortName, SpecLongName })
            {
                var spec = Assert.Single(Requests(record, "POST", $"/deployments/{specName}/validate")).GetProperty("properties");
                Assert.EndsWith(
                    $"/resourceGroups/{TemplateSpecCase.GroupName}/providers/Microsoft.Resources/templateSpecs/{TemplateSpecCase.SpecName}/versions/v1",
                    spec.GetProperty("templateLink").GetProperty("id").GetString());
            }
        }

        private static JsonElement Value(JsonElement parameters, string name) => parameters.GetProperty(name).GetProperty("value");

        private static string[] Sub(Target target, string leaf, params string[] args) =>
            ["deployment", "sub", leaf, "--subscription-id", target.SubscriptionId, .. args];

        private static void AssertValid(IReadOnlyList<object> output)
        {
            var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(output));
            Assert.Null(validation.Error);
            Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);
        }

        private static void AssertValidRest(RestResponse response)
        {
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(State.Succeeded, response.Json?.GetProperty("properties").GetProperty("provisioningState").GetString());
        }

        private static RestResponse Rest(Target target, string method, string uri, string? body = null) =>
            Assert.IsType<RestResponse>(Assert.Single(target.Host.RunCli(
                body is null
                    ? ["rest", "--method", method, "--uri", uri]
                    : ["rest", "--method", method, "--uri", uri, "--body", body])));

        // The body New-AzDeployment and Test-AzDeployment build for the greeting template with
        // -Tag, serialized from the same models with the same options.
        private static string TaggedBody(string template, bool debug)
        {
            var body = new Deployment
            {
                Location = Location,
                Properties = new DeploymentProperties
                {
                    Template = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(template)),
                    Parameters = new JsonObject { ["greeting"] = new JsonObject { ["value"] = Greeting } },
                    DebugSetting = debug ? new DebugSetting { DetailLevel = DebugDetailLevel } : null,
                },
                Tags = new Dictionary<string, string> { ["env"] = "review" },
            };
            return JsonSerializer.Serialize(body, body.GetType(), s_armJson);
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
