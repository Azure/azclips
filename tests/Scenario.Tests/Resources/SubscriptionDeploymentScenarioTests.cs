// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
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
/// Runs <c>deployment sub</c> through the real dispatcher, binders, and ARM pipeline. The test is one
/// recorded session in four parts: the lifecycle, failures, parameters, and template spec.
/// <para>
/// Every template is linked by a URI that pins a GitHub commit, so what ARM deploys never changes
/// between re-records. The lifecycle runs validate, what-if, create, show, list, a filtered list,
/// export, the cancel rejections, delete, and a cancel of an in-flight deployment. The failures part
/// covers an invalid mode, a create that ARM accepts and then fails, and an invalid template through
/// validate, create, and what-if.
/// </para>
/// <para>
/// The parameters part links a remote JSONC parameters file and covers the validation levels and both
/// what-if result formats. Its template writes a policy and a storage account, so the part never
/// creates it. The template spec part deploys a spec version, then one that doesn't exist.
/// </para>
/// <para>
/// Record mode runs the CLI workflow against Azure. Playback replays the recording through the CLI and
/// PowerShell workflows and matches request bodies, so both surfaces must bind their arguments into
/// the same recorded requests. PowerShell has no what-if cmdlet at subscription scope, so those
/// steps run on the CLI only; <c>Save-AzDeploymentTemplate</c> replays the CLI export's request and
/// writes the template to a file. The PowerShell lifecycle's delete, in-flight cancel, and
/// in-flight delete run the real cmdlets by name, piped deployment, and -InputObject, with and without
/// -PassThru; they send the same requests as by name.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public sealed class SubscriptionDeploymentScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string Location = "westus2";
    private const string Mode = "Incremental";

    // The URIs pin commits, so the templates and parameters never change between re-records.
    private const string DocsSamples =
        "https://raw.githubusercontent.com/Azure/azure-docs-json-samples/82e0c6c2bba06ca5615d54751a6b3e67e171383f/azure-resource-manager/functions/";
    private const string CliTests =
        "https://raw.githubusercontent.com/Azure/azure-cli/5c86afc13482f32128dd94e03544b6844ac77f92/src/azure-cli/azure/cli/command_modules/resource/tests/latest/";

    // No resources; outputs its testString parameter ("One Two Three" by default) in lower case.
    private const string LowerTemplateUri = DocsSamples + "string/tolower.json";
    // A resource group template: it validates at subscription scope, but its resourceGroup() output fails the deployment.
    private const string FailingTemplateUri = DocsSamples + "resource/resourcegroup.json";
    // ARM rejects this template as invalid.
    private const string InvalidTemplateUri = CliTests + "param-validation-template.json";
    // Creates a policy, a resource group, and a storage account in it.
    private const string SubscriptionTemplateUri = CliTests + "subscription_level_template.json";
    // A JSONC parameters file that sets only nestedRGName.
    private const string ParametersUri = CliTests + "subscription_level_parameters.json";
    private const string ParametersUriGroupName = "cli_test_subscription_level_deployment";
    private const string QueryString = "sig=abc";

    // Lifecycle
    private const string DeploymentName = "scenario-sub-deployment";
    private const string MissingDeploymentName = "scenario-sub-missing-deployment";
    private const string SlowDeploymentName = "scenario-sub-slow-deployment";
    private const string SlowResourceGroupName = "scenario-sub-slow-rg";

    // Failures
    private const string CreateFailedDeploymentName = "scenario-sub-create-failed-deployment";
    private const string InvalidValidateName = "scenario-sub-invalid-validate";
    private const string CreateInvalidDeploymentName = "scenario-sub-create-invalid-deployment";
    private const string InvalidWhatIfName = "scenario-sub-invalid-what-if";

    // Parameters
    private const string ParamsDeploymentName = "scenario-sub-params";
    private const string ParamsIdsName = "scenario-sub-params-ids";

    // Template spec
    private const string SpecDeploymentName = "scenario-sub-ts-deployment";
    private const string SpecMissingVersionName = "scenario-sub-ts-missing-version";
    private const string SpecGroupName = "scenario-sub-ts-rg";
    private const string SpecName = "scenario-sub-ts-spec";
    private const string SpecApiVersion = "2022-02-01";

    // The bodies below go out through `rest --body` and Invoke-AzRestMethod -Payload, which send them
    // verbatim. Body matching compares them byte for byte with the recording, so they stay on one line.

    // A storage account takes long enough to provision that the deployment is still cancellable. The
    // subscription deployment creates the group, then a nested deployment adds the account to it; the
    // account name derives from the group id, so no run id appears in the template.
    private const string SlowDeploymentBody =
        """{"location":"__LOCATION__","properties":{"mode":"Incremental","parameters":{"groupName":{"value":"__GROUP__"}},"template":{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"groupName":{"type":"string"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('groupName')]","location":"__LOCATION__"},{"type":"Microsoft.Resources/deployments","apiVersion":"2022-09-01","name":"storage","resourceGroup":"[parameters('groupName')]","dependsOn":["[subscriptionResourceId('Microsoft.Resources/resourceGroups', parameters('groupName'))]"],"properties":{"mode":"Incremental","expressionEvaluationOptions":{"scope":"inner"},"template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Storage/storageAccounts","apiVersion":"2023-01-01","name":"[concat('scenario', uniqueString(resourceGroup().id))]","location":"[resourceGroup().location]","sku":{"name":"Standard_LRS"},"kind":"StorageV2"}]}}}]}}}""";

    // The spec takes the parameter that ParametersUri sets and outputs it.
    private const string SpecMainTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"nestedRGName":{"type":"string"}},"resources":[],"outputs":{"nestedRGName":{"type":"string","value":"[parameters('nestedRGName')]"}}}""";
    private const string SpecBody = $$$"""{"location":"{{{Location}}}","properties":{}}""";
    private const string SpecVersionBody = $$$"""{"location":"{{{Location}}}","properties":{"mainTemplate":{{{SpecMainTemplate}}}}}""";

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments, ExtensionUnderTest.Rest];

    [Fact]
    public void ValidateWhatIfCreateShowListExportCancelDeleteSubscriptionDeployment()
    {
        // Trims every deployment list to this run's deployments. It runs before the run-scoped-name
        // sanitizer, so it still sees the record run's suffix.
        var mode = RecorderModeResolver.FromEnvironment();
        var deploymentList = new DeploymentListSanitizer(
            [.. new[] { DeploymentName, SlowDeploymentName }.Select(name => TestRun.ScopedName(name, mode))]);
        var sanitizers = new RecordingSanitizers(
            [deploymentList, .. RecordingSanitizers.Default.Sanitizers],
            [.. RecordingSanitizers.Default.RemovedHeaders]);

        // A method can't share its class's name, so the recording is named explicitly:
        // Records/SubscriptionDeploymentScenarioTests/SubscriptionDeploymentScenarioTests.json.
        var recording = SessionRecordFiles.OpenSession(
            typeof(SubscriptionDeploymentScenarioTests),
            nameof(SubscriptionDeploymentScenarioTests),
            new DefaultRequestMatcher(matchBody: true),
            sanitizers);

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: s_extensions);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunCliWorkflow(host, subscriptionId, recording.Mode);
                AssertRequestBodies(recording.Record);
                recording.Save();
            }
            finally
            {
                // The cleanup runs after the save, so the recording never includes it.
                CleanUp(host, subscriptionId);
            }
            return;
        }

        // Body matching proves each surface sends exactly the recorded requests. These assertions
        // check the values inside them.
        AssertRequestBodies(recording.Record);

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunCliWorkflow(cli, SubscriptionId, recording.Mode);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunPowerShellWorkflow(ps);
        }
    }

    // Record mode only. Deletes what the run created; a step that fails, such as deleting a deployment
    // the workflow never reached, doesn't stop the others.
    private static void CleanUp(ScenarioHost host, string subscriptionId)
    {
        string[] deployments =
        [
            DeploymentName, SlowDeploymentName, CreateFailedDeploymentName, CreateInvalidDeploymentName,
            SpecDeploymentName, SpecMissingVersionName,
        ];
        foreach (var name in deployments)
        {
            try
            {
                host.RunCli("deployment", "sub", "delete", "--subscription-id", subscriptionId, "--name", TestRun.ScopedName(name, RecorderMode.Record));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to clean up deployment '{name}': {exception.Message}");
            }
        }

        // Deleting the template spec's group deletes the spec with it.
        foreach (var group in new[] { SlowResourceGroupName, SpecGroupName })
        {
            try
            {
                host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", TestRun.ScopedName(group, RecorderMode.Record));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to clean up resource group '{group}': {exception.Message}");
            }
        }
    }

    // Record mode runs this against Azure, so names carry the record run's suffix and the
    // subscription is the live one. The run-scoped-name sanitizer strips the suffix from the recording.
    private static void RunCliWorkflow(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        RunCliLifecycle(host, subscriptionId, mode);
        RunCliInFlightCancel(host, subscriptionId, mode);
        RunCliFailures(host, subscriptionId, mode);
        RunCliParameters(host, subscriptionId, mode);
        RunCliTemplateSpec(host, subscriptionId, mode);
    }

    private static void RunCliLifecycle(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(DeploymentName, mode);
        string[] template = ["--template-uri", LowerTemplateUri, "--query-string", QueryString];

        AssertValidatedLower(host.RunCli(CliDeploy("validate", subscriptionId, name, template)));

        AssertNoWhatIfChanges(host.RunCli(CliDeploy("what-if", subscriptionId, name, template)));

        AssertDeployedLower(name, host.RunCli(CliDeploy("create", subscriptionId, name, [.. template, "--validation-level", "Provider"])));

        var shown = AssertDeployedLower(name, host.RunCli(
            "deployment", "sub", "show",
            "--subscription-id", subscriptionId,
            "--name", name));
        Assert.Equal(DeploymentId(subscriptionId, name), shown.Id, ignoreCase: true);

        Assert.Contains(name, DeploymentNames(host.RunCli(
            "deployment", "sub", "list",
            "--subscription-id", subscriptionId)));

        // Every listed deployment matches the filter, and this run's deployment is among them.
        var succeeded = Assert.IsType<List<DeploymentExtended>>(Assert.Single(host.RunCli(
            "deployment", "sub", "list",
            "--subscription-id", subscriptionId,
            "--filter", "provisioningState eq 'Succeeded'")));
        Assert.Contains(name, succeeded.Select(d => d.Name));
        Assert.All(succeeded, d => Assert.Equal(State.Succeeded, d.ProvisioningState));

        var exported = Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
            "deployment", "sub", "export",
            "--subscription-id", subscriptionId,
            "--name", name)));
        Assert.True(exported.GetProperty("outputs").TryGetProperty("toLowerOutput", out _), $"Exported template has no toLowerOutput output: {exported}");

        // A finished deployment can't be canceled, and a missing one isn't found.
        var finished = Assert.Throws<HttpRequestException>(() => host.RunCli(
            "deployment", "sub", "cancel",
            "--subscription-id", subscriptionId,
            "--name", name));
        Assert.Contains("DeploymentCannotBeCancelled", finished.Message);

        var missing = Assert.Throws<HttpRequestException>(() => host.RunCli(
            "deployment", "sub", "cancel",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(MissingDeploymentName, mode)));
        Assert.Contains("DeploymentNotFound", missing.Message);

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", name));

        Assert.DoesNotContain(name, DeploymentNames(host.RunCli(
            "deployment", "sub", "list",
            "--subscription-id", subscriptionId)));
    }

    // `deployment sub create` blocks until the deployment is terminal, so the deployment to cancel
    // starts with a raw PUT that returns as soon as ARM accepts it.
    private static void RunCliInFlightCancel(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(SlowDeploymentName, mode);
        IReadOnlyList<object> Show() => host.RunCli(
            "deployment", "sub", "show",
            "--subscription-id", subscriptionId,
            "--name", name);

        Assert.Equal(201, RestStatusCode(host.RunCli(
            "rest",
            "--method", "PUT",
            "--uri", DeploymentUri(subscriptionId, name),
            "--body", SlowDeploymentBodyFor(TestRun.ScopedName(SlowResourceGroupName, mode)))));

        AssertCancellable(WaitForState(Show, mode, attempts: 15, State.Accepted, State.Running));

        CancelInFlight(() => Assert.Empty(host.RunCli(
            "deployment", "sub", "cancel",
            "--subscription-id", subscriptionId,
            "--name", name)));

        Assert.Equal(State.Canceled, WaitForState(Show, mode, attempts: 30, State.Canceled).ProvisioningState);

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", name));
    }

    private static void RunCliFailures(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        // An invalid mode throws before the command sends any request. Playback fails on an
        // unrecorded request, so a passing run also proves nothing reached ARM.
        AssertInvalidMode(Assert.ThrowsAny<Exception>(() => host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(MissingDeploymentName, mode),
            "--location", Location,
            "--mode", "Bogus",
            "--template-uri", LowerTemplateUri)));

        // ARM accepts the deployment, then fails it.
        var createFailedName = TestRun.ScopedName(CreateFailedDeploymentName, mode);
        AssertOperationFailed(Assert.Throws<Exception>(() => host.RunCli(
            CliDeploy("create", subscriptionId, createFailedName, "--template-uri", FailingTemplateUri))));

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", createFailedName));

        // ARM rejects the invalid template in the validate and create requests themselves, so nothing is created.
        AssertInvalidTemplate(Assert.Throws<HttpRequestException>(() => host.RunCli(
            CliDeploy("validate", subscriptionId, TestRun.ScopedName(InvalidValidateName, mode), "--template-uri", InvalidTemplateUri))));

        AssertInvalidTemplate(Assert.Throws<HttpRequestException>(() => host.RunCli(
            CliDeploy("create", subscriptionId, TestRun.ScopedName(CreateInvalidDeploymentName, mode), "--template-uri", InvalidTemplateUri))));

        // The what-if POST returns 202, and the polled result reports the failure.
        AssertOperationFailed(Assert.Throws<Exception>(() => host.RunCli(
            CliDeploy("what-if", subscriptionId, TestRun.ScopedName(InvalidWhatIfName, mode), "--template-uri", InvalidTemplateUri))));
    }

    // ARM resolves nestedRGName from the remote parameters file. Each request sends a different
    // validation level or what-if result format.
    private static void RunCliParameters(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(ParamsDeploymentName, mode);
        string[] template = ["--template-uri", SubscriptionTemplateUri, "--parameters-uri", ParametersUri];

        AssertValidatedFromParametersUri(host.RunCli(CliDeploy("validate", subscriptionId, name, [.. template, "--validation-level", "Template"])));

        // FullResourcePayloads, the default, includes the resource payload; ResourceIdOnly includes only the id.
        var full = AssertCreatesNestedGroup(host.RunCli(CliDeploy("what-if", subscriptionId, name, [.. template, "--validation-level", "ProviderNoRbac"])));
        Assert.NotNull(full.After);

        var idOnly = AssertCreatesNestedGroup(host.RunCli(CliDeploy(
            "what-if", subscriptionId, TestRun.ScopedName(ParamsIdsName, mode), [.. template, "--result-format", "ResourceIdOnly"])));
        Assert.Null(idOnly.After);
    }

    // The resource group holds the template spec. No command creates a spec, so raw PUTs create the
    // spec and its v1 version. The last create references a v2 that doesn't exist.
    private static void RunCliTemplateSpec(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(SpecDeploymentName, mode);
        var group = TestRun.ScopedName(SpecGroupName, mode);
        var specUrl = SpecUrl(subscriptionId, group);
        var specId = SpecVersionId(subscriptionId, group, "v1");
        string[] template = ["--template-spec-id", specId, "--parameters-uri", ParametersUri];

        Assert.Single(host.RunCli(
            "group", "create",
            "--subscription-id", subscriptionId,
            "--name", group,
            "--location", Location));

        AssertPutSucceeded(host.RunCli(
            "rest",
            "--method", "PUT",
            "--uri", $"{specUrl}?api-version={SpecApiVersion}",
            "--body", SpecBody));

        AssertPutSucceeded(host.RunCli(
            "rest",
            "--method", "PUT",
            "--uri", $"{specUrl}/versions/v1?api-version={SpecApiVersion}",
            "--body", SpecVersionBody));

        AssertValidatedFromParametersUri(host.RunCli(CliDeploy("validate", subscriptionId, name, template)));

        AssertNoWhatIfChanges(host.RunCli(CliDeploy("what-if", subscriptionId, name, template)));

        AssertDeployedFromSpec(name, specId, host.RunCli(CliDeploy("create", subscriptionId, name, template)));

        AssertDeployedFromSpec(name, specId, host.RunCli(
            "deployment", "sub", "show",
            "--subscription-id", subscriptionId,
            "--name", name));

        var exported = Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
            "deployment", "sub", "export",
            "--subscription-id", subscriptionId,
            "--name", name)));
        Assert.True(exported.GetProperty("outputs").TryGetProperty("nestedRGName", out _), $"Exported template has no nestedRGName output: {exported}");

        AssertMissingSpecVersion(Assert.ThrowsAny<Exception>(() => host.RunCli(CliDeploy(
            "create", subscriptionId, TestRun.ScopedName(SpecMissingVersionName, mode),
            "--template-spec-id", SpecVersionId(subscriptionId, group, "v2"), "--parameters-uri", ParametersUri))));

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", name));
    }

    // PowerShell runs in playback only, so names are the base names and ids carry the placeholder
    // subscription. It has no subscription what-if, so it replays the rest of the
    // CLI's requests in the same order.
    private static void RunPowerShellWorkflow(ScenarioHost host)
    {
        RunPowerShellLifecycle(host);
        RunPowerShellInFlightCancel(host);
        RunPowerShellFailures(host);
        RunPowerShellParameters(host);
        RunPowerShellTemplateSpec(host);
    }

    private static void RunPowerShellLifecycle(ScenarioHost host)
    {
        (string, object)[] template = [("TemplateUri", LowerTemplateUri), ("QueryString", QueryString)];

        AssertValidatedLower(host.RunPowerShell("Test-AzDeployment", PsDeploy(DeploymentName, template)));

        AssertDeployedLower(DeploymentName, host.RunPowerShell("New-AzDeployment", PsDeploy(DeploymentName, [.. template, ("ValidationLevel", "Provider")])));

        var shown = AssertDeployedLower(DeploymentName, host.RunPowerShell("Get-AzDeployment_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
        }));
        Assert.Equal(DeploymentId(SubscriptionId, DeploymentName), shown.Id, ignoreCase: true);

        Assert.Contains(DeploymentName, DeploymentNames(host.RunPowerShell("Get-AzDeployment_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
        })));

        // Same export POST as the CLI; the cmdlet writes the template into an existing directory as "<name>.json".
        var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(host.RunPowerShellCmdlet<SaveDeploymentTemplateCmdlet>(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
            ["Path"] = _directory,
        })));
        Assert.Equal(Path.Combine(_directory, DeploymentName + ".json"), saved.Path);
        using (var savedTemplate = JsonDocument.Parse(File.ReadAllText(saved.Path)))
        {
            Assert.Contains("subscriptionDeploymentTemplate", savedTemplate.RootElement.GetProperty("$schema").GetString());
            Assert.True(savedTemplate.RootElement.GetProperty("outputs").TryGetProperty("greeting", out _), $"Saved template has no greeting output: {savedTemplate.RootElement}");
        }

        // A finished deployment can't be stopped, and a missing one isn't found.
        var finished = Assert.Throws<HttpRequestException>(() => host.RunPowerShell("Stop-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
        }));
        Assert.Contains("DeploymentCannotBeCancelled", finished.Message);

        var missing = Assert.Throws<HttpRequestException>(() => host.RunPowerShell("Stop-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = MissingDeploymentName,
        }));
        Assert.Contains("DeploymentNotFound", missing.Message);

        // Same DELETE as without the switch; -PassThru writes True.
        Assert.True(Assert.IsType<bool>(Assert.Single(host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
            ["PassThru"] = true,
        }))));

        Assert.DoesNotContain(DeploymentName, DeploymentNames(host.RunPowerShell("Get-AzDeployment_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
        })));
    }

    private static void RunPowerShellInFlightCancel(ScenarioHost host)
    {
        IReadOnlyList<object> Show() => host.RunPowerShell("Get-AzDeployment_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SlowDeploymentName,
        });

        Assert.Equal(201, RestStatusCode(host.RunPowerShell("Invoke-AzRestMethod", new Dictionary<string, object>
        {
            ["Method"] = "PUT",
            ["Uri"] = DeploymentUri(SubscriptionId, SlowDeploymentName),
            ["Payload"] = SlowDeploymentBodyFor(SlowResourceGroupName),
        })));

        var inFlight = WaitForState(Show, RecorderMode.Playback, attempts: 15, State.Accepted, State.Running);
        AssertCancellable(inFlight);

        // Same cancel POST as by name: the piped deployment's Id selects it, and -PassThru writes True.
        CancelInFlight(() => Assert.True(Assert.IsType<bool>(Assert.Single(host.RunPowerShellCmdlet<StopDeploymentCmdlet>(
            new Dictionary<string, object> { ["PassThru"] = true },
            pipelineInput: inFlight)))));

        var canceled = WaitForState(Show, RecorderMode.Playback, attempts: 30, State.Canceled);
        Assert.Equal(State.Canceled, canceled.ProvisioningState);

        // -InputObject takes the deployment's Id; without -PassThru nothing is written.
        Assert.Empty(host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(new Dictionary<string, object>
        {
            ["InputObject"] = canceled,
        }));
    }

    private static void RunPowerShellFailures(ScenarioHost host)
    {
        // PowerShell's ValidateSet rejects the mode while binding, before the engine runs.
        var invalidMode = Assert.ThrowsAny<ParameterBindingException>(() => host.RunPowerShellCmdlet<NewDeploymentCmdlet>(
            PsDeploy(MissingDeploymentName, ("Mode", "Bogus"), ("TemplateUri", LowerTemplateUri))));
        Assert.Contains("Cannot validate argument on parameter 'Mode'", invalidMode.Message);

        // Only PowerShell stops and removes a piped deployment, whose Id the cmdlet parses into the
        // subscription and name. A resource group deployment's id is the wrong scope.
        string groupScopedId = $"/subscriptions/{SubscriptionId}/resourceGroups/group/providers/Microsoft.Resources/deployments/{MissingDeploymentName}";
        var piped = new Dictionary<string, object> { ["InputObject"] = new DeploymentExtended { Id = groupScopedId } };
        var stop = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<StopDeploymentCmdlet>(piped));
        var remove = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(piped));
        Assert.All(new[] { stop, remove }, error =>
        {
            Assert.Contains("Invalid deployment id", error.Message);
            Assert.Contains("/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}", error.Message);
        });

        AssertOperationFailed(Assert.Throws<Exception>(() => host.RunPowerShell("New-AzDeployment",
            PsDeploy(CreateFailedDeploymentName, ("TemplateUri", FailingTemplateUri)))));

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = CreateFailedDeploymentName,
        }));

        AssertInvalidTemplate(Assert.Throws<HttpRequestException>(() => host.RunPowerShell("Test-AzDeployment",
            PsDeploy(InvalidValidateName, ("TemplateUri", InvalidTemplateUri)))));

        AssertInvalidTemplate(Assert.Throws<HttpRequestException>(() => host.RunPowerShell("New-AzDeployment",
            PsDeploy(CreateInvalidDeploymentName, ("TemplateUri", InvalidTemplateUri)))));
    }

    // The same validate as the CLI; the what-if steps have no PowerShell cmdlet.
    private static void RunPowerShellParameters(ScenarioHost host) =>
        AssertValidatedFromParametersUri(host.RunPowerShell("Test-AzDeployment", PsDeploy(
            ParamsDeploymentName, ("TemplateUri", SubscriptionTemplateUri), ("ParametersUri", ParametersUri), ("ValidationLevel", "Template"))));

    private static void RunPowerShellTemplateSpec(ScenarioHost host)
    {
        string specUrl = SpecUrl(SubscriptionId, SpecGroupName);
        string specId = SpecVersionId(SubscriptionId, SpecGroupName, "v1");
        (string, object)[] template = [("TemplateSpecId", specId), ("ParametersUri", ParametersUri)];

        Assert.Single(host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecGroupName,
            ["Location"] = Location,
        }));

        AssertPutSucceeded(host.RunPowerShell("Invoke-AzRestMethod", new Dictionary<string, object>
        {
            ["Method"] = "PUT",
            ["Uri"] = $"{specUrl}?api-version={SpecApiVersion}",
            ["Payload"] = SpecBody,
        }));

        AssertPutSucceeded(host.RunPowerShell("Invoke-AzRestMethod", new Dictionary<string, object>
        {
            ["Method"] = "PUT",
            ["Uri"] = $"{specUrl}/versions/v1?api-version={SpecApiVersion}",
            ["Payload"] = SpecVersionBody,
        }));

        AssertValidatedFromParametersUri(host.RunPowerShell("Test-AzDeployment", PsDeploy(SpecDeploymentName, template)));

        AssertDeployedFromSpec(SpecDeploymentName, specId, host.RunPowerShell("New-AzDeployment", PsDeploy(SpecDeploymentName, template)));

        AssertDeployedFromSpec(SpecDeploymentName, specId, host.RunPowerShell("Get-AzDeployment_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecDeploymentName,
        }));

        AssertMissingSpecVersion(Assert.ThrowsAny<Exception>(() => host.RunPowerShell("New-AzDeployment", PsDeploy(
            SpecMissingVersionName, ("TemplateSpecId", SpecVersionId(SubscriptionId, SpecGroupName, "v2")), ("ParametersUri", ParametersUri)))));

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecDeploymentName,
        }));
    }

    /// <summary>The arguments every validate, what-if, and create sends, then the template arguments.</summary>
    private static string[] CliDeploy(string verb, string subscriptionId, string name, params string[] template) =>
    [
        "deployment", "sub", verb,
        "--subscription-id", subscriptionId,
        "--name", name,
        "--location", Location,
        "--mode", Mode,
        .. template,
    ];

    /// <summary>The PowerShell twin of <see cref="CliDeploy"/>; a later entry replaces an earlier one with the same key.</summary>
    private static Dictionary<string, object> PsDeploy(string name, params (string Key, object Value)[] template)
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = name,
            ["Location"] = Location,
            ["Mode"] = Mode,
        };
        foreach (var (key, value) in template)
        {
            parameters[key] = value;
        }
        return parameters;
    }

    private static DeploymentValidateResult AssertValidated(IReadOnlyList<object> outputs)
    {
        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(outputs));
        Assert.Null(validation.Error);
        Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);
        return validation;
    }

    private static void AssertValidatedLower(IReadOnlyList<object> outputs) =>
        Assert.Equal("One Two Three", AssertValidated(outputs).Properties?.Parameters?["testString"].Value);

    private static void AssertValidatedFromParametersUri(IReadOnlyList<object> outputs) =>
        Assert.Equal(ParametersUriGroupName, AssertValidated(outputs).Properties?.Parameters?["nestedRGName"].Value);

    private static WhatIfOperationResult AssertWhatIfSucceeded(IReadOnlyList<object> outputs)
    {
        var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(outputs));
        Assert.Equal(State.Succeeded, whatIf.Status);
        Assert.Null(whatIf.Error);
        return whatIf;
    }

    private static void AssertNoWhatIfChanges(IReadOnlyList<object> outputs) =>
        Assert.Empty(AssertWhatIfSucceeded(outputs).Properties?.Changes ?? []);

    /// <summary>Asserts every change is a Create, and returns the change for the parameters file's resource group.</summary>
    private static WhatIfChange AssertCreatesNestedGroup(IReadOnlyList<object> outputs)
    {
        var changes = AssertWhatIfSucceeded(outputs).Properties?.Changes ?? [];
        Assert.All(changes, change => Assert.Equal("Create", change.ChangeType));
        var group = Assert.Single(changes, change =>
            change.ResourceId?.EndsWith($"/resourceGroups/{ParametersUriGroupName}", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Null(group.Before);
        return group;
    }

    private static DeploymentExtended AssertDeployed(string name, IReadOnlyList<object> outputs)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(outputs));
        Assert.Equal(name, deployment.Name);
        Assert.Equal(State.Succeeded, deployment.ProvisioningState);
        return deployment;
    }

    private static DeploymentExtended AssertDeployedLower(string name, IReadOnlyList<object> outputs)
    {
        var deployment = AssertDeployed(name, outputs);
        Assert.Equal("one two three", deployment.Properties?.Outputs?["toLowerOutput"].Value);
        Assert.Equal(LowerTemplateUri, deployment.Properties?.TemplateLink?.Uri);
        return deployment;
    }

    /// <summary>Asserts the deployment used the parameters file and links back to the spec version.</summary>
    private static void AssertDeployedFromSpec(string name, string specId, IReadOnlyList<object> outputs)
    {
        var deployment = AssertDeployed(name, outputs);
        Assert.Equal(ParametersUriGroupName, deployment.Properties?.Outputs?["nestedRGName"].Value);
        Assert.Equal(specId, deployment.Properties?.TemplateLink?.Id, ignoreCase: true);
    }

    private static void AssertInvalidMode(Exception error) =>
        Assert.Contains("Parameter 'mode' value 'Bogus' is not allowed", error.Message, StringComparison.OrdinalIgnoreCase);

    // A failed long-running operation reports only the polled response's status and headers.
    private static void AssertOperationFailed(Exception error) =>
        Assert.StartsWith("Long running operation failed", error.Message);

    private static void AssertInvalidTemplate(HttpRequestException error)
    {
        Assert.StartsWith("400", error.Message);
        Assert.Contains("InvalidTemplate", error.Message);
    }

    private static void AssertMissingSpecVersion(Exception error) =>
        Assert.Contains("InvalidTemplateSpec", error.ToString());

    private static void AssertCancellable(DeploymentExtended deployment)
    {
        string? state = deployment.ProvisioningState;
        Assert.True(state is State.Accepted or State.Running, $"The deployment to cancel is '{state}', not cancellable; re-record this test.");
    }

    private static void CancelInFlight(Action cancel)
    {
        try
        {
            cancel();
        }
        catch (HttpRequestException error) when (error.Message.Contains("DeploymentCannotBeCancelled"))
        {
            Assert.Fail("The deployment finished before it could be cancelled; re-record this test.");
        }
    }

    /// <summary>
    /// Shows the deployment until it reaches a wanted state or any terminal one. Playback replays the
    /// recorded responses in order, so it takes the same number of polls; only record mode waits between them.
    /// </summary>
    private static DeploymentExtended WaitForState(Func<IReadOnlyList<object>> show, RecorderMode mode, int attempts, params string[] wanted)
    {
        DeploymentExtended? deployment = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            deployment = Assert.IsType<DeploymentExtended>(Assert.Single(show()));
            string? state = deployment.ProvisioningState;
            if (wanted.Contains(state) || state is State.Succeeded or State.Failed or State.Canceled)
            {
                break;
            }

            if (mode == RecorderMode.Record)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
        return Assert.IsType<DeploymentExtended>(deployment);
    }

    // The list command writes every page as one collected List<DeploymentExtended>.
    private static List<string?> DeploymentNames(IReadOnlyList<object> outputs) =>
        [.. Assert.IsType<List<DeploymentExtended>>(Assert.Single(outputs)).Select(d => d.Name)];

    private static int RestStatusCode(IReadOnlyList<object> outputs) =>
        Assert.IsType<RestResponse>(Assert.Single(outputs)).StatusCode;

    private static void AssertPutSucceeded(IReadOnlyList<object> outputs)
    {
        int statusCode = RestStatusCode(outputs);
        Assert.True(statusCode is 200 or 201, $"PUT returned {statusCode}.");
    }

    private static string DeploymentId(string subscriptionId, string name) =>
        $"/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{name}";

    private static string DeploymentUri(string subscriptionId, string name) =>
        $"https://management.azure.com{DeploymentId(subscriptionId, name)}?api-version=2025-04-01";

    private static string SlowDeploymentBodyFor(string group) =>
        SlowDeploymentBody.Replace("__LOCATION__", Location).Replace("__GROUP__", group);

    private static string SpecUrl(string subscriptionId, string group) =>
        $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{group}/providers/Microsoft.Resources/templateSpecs/{SpecName}";

    private static string SpecVersionId(string subscriptionId, string group, string version) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{group}/providers/Microsoft.Resources/templateSpecs/{SpecName}/versions/{version}";

    /// <summary>
    /// Checks the values inside the recorded requests. The run-scoped-name sanitizer strips the record run's
    /// suffix from every name, so the bodies carry the base names.
    /// </summary>
    private static void AssertRequestBodies(SessionRecord record)
    {
        // The lifecycle links the template with its query string; only the create sends a validation level.
        var lifecycle = ValidateWhatIfCreateBodies(record, DeploymentName);
        foreach (var body in lifecycle)
        {
            var link = AssertDeploymentBody(body).GetProperty("templateLink");
            Assert.Equal(LowerTemplateUri, link.GetProperty("uri").GetString());
            Assert.Equal(QueryString, link.GetProperty("queryString").GetString());
        }
        Assert.Equal("Provider", lifecycle[2].GetProperty("properties").GetProperty("validationLevel").GetString());

        var slow = Assert.Single(Requests(record, "PUT", $"/deployments/{SlowDeploymentName}?"));
        Assert.Equal(
            SlowResourceGroupName,
            slow.GetProperty("properties").GetProperty("parameters").GetProperty("groupName").GetProperty("value").GetString());

        // The lifecycle's unfiltered lists share this URI, so check that exactly one list sent the filter.
        Assert.Single(record.Entries, e => IsRequest(e, "GET", "/providers/Microsoft.Resources/deployments?")
            && Uri.UnescapeDataString(e.Request.Uri).Contains("filter=provisioningState", StringComparison.Ordinal));

        // The invalid mode sends nothing; the invalid template's create still sends its PUT, which ARM rejects.
        Assert.Empty(Requests(record, "PUT", $"/deployments/{MissingDeploymentName}?"));
        var invalid = AssertDeploymentBody(Assert.Single(Requests(record, "PUT", $"/deployments/{CreateInvalidDeploymentName}?")));
        Assert.Equal(InvalidTemplateUri, invalid.GetProperty("templateLink").GetProperty("uri").GetString());

        var paramsValidate = AssertParametersBody(Assert.Single(Requests(record, "POST", $"/deployments/{ParamsDeploymentName}/validate")));
        Assert.Equal("Template", paramsValidate.GetProperty("validationLevel").GetString());
        var paramsWhatIf = AssertParametersBody(Assert.Single(Requests(record, "POST", $"/deployments/{ParamsDeploymentName}/whatIf")));
        Assert.Equal("ProviderNoRbac", paramsWhatIf.GetProperty("validationLevel").GetString());
        var idOnly = AssertParametersBody(Assert.Single(Requests(record, "POST", $"/deployments/{ParamsIdsName}/whatIf")));
        Assert.Equal("ResourceIdOnly", idOnly.GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());

        foreach (var body in ValidateWhatIfCreateBodies(record, SpecDeploymentName))
        {
            AssertSpecBody(body, "v1");
        }
        AssertSpecBody(Assert.Single(Requests(record, "PUT", $"/deployments/{SpecMissingVersionName}?")), "v2");
    }

    /// <summary>Asserts the location and mode every deployment request sends, and returns its properties.</summary>
    private static JsonElement AssertDeploymentBody(JsonElement body)
    {
        Assert.Equal(Location, body.GetProperty("location").GetString());
        var properties = body.GetProperty("properties");
        Assert.Equal(Mode, properties.GetProperty("mode").GetString());
        return properties;
    }

    private static JsonElement AssertParametersBody(JsonElement body)
    {
        var properties = AssertDeploymentBody(body);
        Assert.Equal(SubscriptionTemplateUri, properties.GetProperty("templateLink").GetProperty("uri").GetString());
        Assert.Equal(ParametersUri, properties.GetProperty("parametersLink").GetProperty("uri").GetString());
        return properties;
    }

    private static void AssertSpecBody(JsonElement body, string version)
    {
        var properties = AssertDeploymentBody(body);
        Assert.EndsWith(
            $"/resourceGroups/{SpecGroupName}/providers/Microsoft.Resources/templateSpecs/{SpecName}/versions/{version}",
            properties.GetProperty("templateLink").GetProperty("id").GetString());
        Assert.Equal(ParametersUri, properties.GetProperty("parametersLink").GetProperty("uri").GetString());
    }

    /// <summary>Returns the bodies of the deployment's validate, what-if, and create PUT, in that order.</summary>
    private static JsonElement[] ValidateWhatIfCreateBodies(SessionRecord record, string name) =>
    [
        Assert.Single(Requests(record, "POST", $"/deployments/{name}/validate")),
        Assert.Single(Requests(record, "POST", $"/deployments/{name}/whatIf")),
        Assert.Single(Requests(record, "PUT", $"/deployments/{name}?")),
    ];

    /// <summary>Returns the body of every recorded request that matches the method and URI, in request order.</summary>
    private static List<JsonElement> Requests(SessionRecord record, string method, string uriPart) =>
        [.. record.Entries
            .Where(e => IsRequest(e, method, uriPart))
            .Select(e =>
            {
                Assert.NotNull(e.Request.Body);
                using var document = JsonDocument.Parse(e.Request.Body);
                return document.RootElement.Clone();
            })];

    private static bool IsRequest(SessionRecordEntry entry, string method, string uriPart) =>
        entry.Request.Method == method && entry.Request.Uri.Contains(uriPart, StringComparison.OrdinalIgnoreCase);

    // ARM deployment provisioning states this scenario asserts on.
    private static class State
    {
        public const string Accepted = "Accepted";
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string Canceled = "Canceled";
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
