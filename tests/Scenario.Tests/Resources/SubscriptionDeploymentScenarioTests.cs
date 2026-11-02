// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
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
/// Runs <c>deployment sub</c> through the real dispatcher, binders, and ARM pipeline. The test is one
/// recorded session in five parts: the lifecycle, parameters, what-if, template spec, and Bicep.
/// <para>
/// The lifecycle runs validate, what-if, create, wait, show, list, export, the cancel rejections,
/// delete, and a cancel of an in-flight deployment. Create validates the deployment before its PUT. It
/// also covers invalid arguments, wait timeouts, a deployment that fails, a create that fails
/// validation without a PUT, and a what-if whose polled result fails.
/// </para>
/// <para>
/// The parameters part overlays a JSONC parameters file with inline parameters, and covers a remote
/// parameters URI, explicit validation levels, show by resource id, and a filtered list. The what-if
/// part covers a Modify delta, a short-circuited evaluation, and both result formats. The template
/// spec part deploys a spec version, then one that doesn't exist. The Bicep part deploys a compiled
/// Bicep file, then one whose nullable parameters are all omitted.
/// </para>
/// <para>
/// Record mode runs the CLI workflow against Azure. Playback replays the recording through the CLI and
/// PowerShell workflows and matches request bodies, so both surfaces must bind their arguments into
/// the same recorded requests. PowerShell has no what-if, wait, or export cmdlet at subscription
/// scope, so those steps run on the CLI only. The PowerShell lifecycle's delete, in-flight cancel, and
/// in-flight delete run the real cmdlets by name, piped deployment, and -InputObject, with and without
/// -PassThru; they send the same requests as by name.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public sealed class SubscriptionDeploymentScenarioTests : IDisposable
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string Location = "westus2";

    // Lifecycle
    private const string DeploymentName = "scenario-sub-deployment";
    private const string MissingDeploymentName = "scenario-sub-missing-deployment";
    private const string SlowDeploymentName = "scenario-sub-slow-deployment";
    private const string SlowResourceGroupName = "scenario-sub-slow-rg";
    private const string FailedDeploymentName = "scenario-sub-failed-deployment";
    private const string InvalidWhatIfName = "scenario-sub-invalid-what-if";
    private const string CreateFailedDeploymentName = "scenario-sub-create-failed-deployment";
    private const string CreateInvalidDeploymentName = "scenario-sub-create-invalid-deployment";
    private const string Greeting = "hello";

    // Parameters
    private const string ParamsDeploymentName = "scenario-sub-params";
    private const string ParamsUriDeploymentName = "scenario-sub-params-uri";
    private const string OverlayGreeting = "overlay";
    private const string FileGroupName = "file-group";
    // The URI pins a commit, so the parameters the deployment resolves never change between re-records.
    private const string ParametersUri =
        "https://raw.githubusercontent.com/Azure/azure-cli/5c86afc13482f32128dd94e03544b6844ac77f92/src/azure-cli/azure/cli/command_modules/resource/tests/latest/subscription_level_parameters.json";
    private const string ParametersUriGroupName = "cli_test_subscription_level_deployment";

    // What-if
    private const string WhatIfSetupName = "scenario-sub-whatif-setup";
    private const string WhatIfModifyName = "scenario-sub-whatif-modify";
    private const string WhatIfShortCircuitName = "scenario-sub-whatif-short-circuit";
    private const string WhatIfCreateName = "scenario-sub-whatif-create";
    private const string WhatIfResourceIdOnlyName = "scenario-sub-whatif-ids";
    private const string WhatIfGroupName = "scenario-sub-whatif-rg";
    private const string WhatIfNewGroupName = "scenario-sub-whatif-new-rg";
    private const string WhatIfNestedDeploymentName = "scenario-sub-whatif-nested";

    // Template spec
    private const string SpecDeploymentName = "scenario-sub-ts-deployment";
    private const string SpecMissingVersionName = "scenario-sub-ts-missing-version";
    private const string SpecGroupName = "scenario-sub-ts-rg";
    private const string SpecName = "scenario-sub-ts-spec";
    private const string SpecApiVersion = "2022-02-01";
    private const string SpecGreeting = "from-spec";

    // Bicep
    private const string BicepDeploymentName = "scenario-sub-bicep";
    private const string BicepNullableDeploymentName = "scenario-sub-bicep-nullable";
    private const string BicepGreeting = "from-bicep";

    // The bodies below go out through `rest --body` and Invoke-AzRestMethod -Payload, which send them
    // verbatim. Body matching compares them byte for byte with the recording, so they stay on one line.

    // A storage account takes long enough to provision that the deployment is still cancellable. The
    // subscription deployment creates the group, then a nested deployment adds the account to it; the
    // account name derives from the group id, so no run id appears in the template.
    private const string SlowDeploymentBody =
        """{"location":"__LOCATION__","properties":{"mode":"Incremental","parameters":{"groupName":{"value":"__GROUP__"}},"template":{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"groupName":{"type":"string"}},"resources":[{"type":"Microsoft.Resources/resourceGroups","apiVersion":"2022-09-01","name":"[parameters('groupName')]","location":"__LOCATION__"},{"type":"Microsoft.Resources/deployments","apiVersion":"2022-09-01","name":"storage","resourceGroup":"[parameters('groupName')]","dependsOn":["[subscriptionResourceId('Microsoft.Resources/resourceGroups', parameters('groupName'))]"],"properties":{"mode":"Incremental","expressionEvaluationOptions":{"scope":"inner"},"template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Storage/storageAccounts","apiVersion":"2023-01-01","name":"[concat('scenario', uniqueString(resourceGroup().id))]","location":"[resourceGroup().location]","sku":{"name":"Standard_LRS"},"kind":"StorageV2"}]}}}]}}}""";

    // The output references a resource group that doesn't exist. ARM accepts the deployment and only
    // fails it when it evaluates the output.
    private const string FailedDeploymentBody =
        """{"location":"__LOCATION__","properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[],"outputs":{"missing":{"type":"string","value":"[reference(subscriptionResourceId('Microsoft.Resources/resourceGroups', 'scenario-sub-missing-rg'), '2022-09-01').location]"}}}}}""";

    // The spec version's main template is the same template as template.json.
    private const string SpecMainTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"greeting":{"type":"string"}},"resources":[],"outputs":{"greeting":{"type":"string","value":"[parameters('greeting')]"}}}""";
    private const string SpecBody = $$$"""{"location":"{{{Location}}}","properties":{}}""";
    private const string SpecVersionBody = $$$"""{"location":"{{{Location}}}","properties":{"mainTemplate":{{{SpecMainTemplate}}}}}""";

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments, ExtensionUnderTest.Rest];

    // Bicep stamps its version and a template hash into the compiled template, which would break body
    // matching whenever playback runs a different Bicep build than the recording.
    private static readonly IRecordingSanitizer s_bicepGeneratorSanitizer =
        new RegexSanitizer("bicep-generator", "\"_generator\"\\s*:\\s*\\{[^{}]*\\}", "\"_generator\":{}");

    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly string _templateFile;
    private readonly string _invalidTemplateFile;
    private readonly string _failingTemplateFile;
    private readonly string _paramsTemplateFile;
    private readonly string _paramsUriTemplateFile;
    private readonly string _parametersFile;
    private readonly string _taggedGroupTemplateFile;
    private readonly string _shortCircuitTemplateFile;
    private readonly string _specParametersFile;
    private readonly string _bicepFile;
    private readonly string _nullableBicepFile;

    public SubscriptionDeploymentScenarioTests()
    {
        _templateFile = WriteFile("template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "greeting": {
                  "type": "string"
                }
              },
              "resources": [],
              "outputs": {
                "greeting": {
                  "type": "string",
                  "value": "[parameters('greeting')]"
                }
              }
            }
            """);

        // Passes the client-side checks, but ARM rejects the undeclared parameter. The what-if POST
        // returns 202 and the polled result comes back Failed; the create fails in its validate POST
        // and sends no PUT.
        _invalidTemplateFile = WriteFile("invalid-template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "resources": [
                {
                  "type": "Microsoft.Resources/resourceGroups",
                  "apiVersion": "2022-09-01",
                  "name": "[parameters('undeclared')]",
                  "location": "westus2"
                }
              ]
            }
            """);

        // ARM accepts the PUT and only fails the deployment when it evaluates the output, which
        // references a resource group that doesn't exist.
        _failingTemplateFile = WriteFile("failing-template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "resources": [],
              "outputs": {
                "missing": {
                  "type": "string",
                  "value": "[reference(subscriptionResourceId('Microsoft.Resources/resourceGroups', 'scenario-sub-missing-rg'), '2022-09-01').location]"
                }
              }
            }
            """);

        _paramsTemplateFile = WriteFile("params-template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "greeting": {
                  "type": "string"
                },
                "nestedRGName": {
                  "type": "string"
                }
              },
              "resources": [],
              "outputs": {
                "greeting": {
                  "type": "string",
                  "value": "[parameters('greeting')]"
                },
                "nestedRGName": {
                  "type": "string",
                  "value": "[parameters('nestedRGName')]"
                }
              }
            }
            """);

        // Declares only the parameter the pinned parameters URI supplies.
        _paramsUriTemplateFile = WriteFile("params-uri-template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "nestedRGName": {
                  "type": "string"
                }
              },
              "resources": [],
              "outputs": {
                "nestedRGName": {
                  "type": "string",
                  "value": "[parameters('nestedRGName')]"
                }
              }
            }
            """);

        // The comments and trailing comma check that the builder accepts JSONC parameters files. The
        // inline greeting overrides the file's greeting.
        _parametersFile = WriteFile("parameters.json", """
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
            """);

        _taggedGroupTemplateFile = WriteFile("tagged-group-template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "groupName": {
                  "type": "string"
                },
                "tagValue": {
                  "type": "string"
                }
              },
              "resources": [
                {
                  "type": "Microsoft.Resources/resourceGroups",
                  "apiVersion": "2022-09-01",
                  "name": "[parameters('groupName')]",
                  "location": "westus2",
                  "tags": {
                    "a": "[parameters('tagValue')]"
                  }
                }
              ]
            }
            """);

        // random defaults to a utcNow() comparison, which what-if can't evaluate. So the conditional
        // group becomes a potential change, the group whose name depends on random is unsupported, and
        // the nested deployment that reads random is short-circuited.
        _shortCircuitTemplateFile = WriteFile("short-circuit-template.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "groupName": {
                  "type": "string"
                },
                "random": {
                  "type": "bool",
                  "defaultValue": "[greater(int(utcNow('%f')), 4)]"
                }
              },
              "resources": [
                {
                  "type": "Microsoft.Resources/resourceGroups",
                  "apiVersion": "2022-09-01",
                  "name": "[parameters('groupName')]",
                  "location": "westus2",
                  "condition": "[parameters('random')]",
                  "tags": {
                    "a": "three"
                  }
                },
                {
                  "type": "Microsoft.Resources/resourceGroups",
                  "apiVersion": "2022-09-01",
                  "name": "[format('{0}-{1}', parameters('groupName'), if(parameters('random'), 'two', 'three'))]",
                  "location": "westus2"
                },
                {
                  "type": "Microsoft.Resources/deployments",
                  "apiVersion": "2024-11-01",
                  "name": "scenario-sub-whatif-nested",
                  "location": "[deployment().location]",
                  "properties": {
                    "mode": "Incremental",
                    "template": {
                      "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                      "contentVersion": "1.0.0.0",
                      "resources": [],
                      "variables": {
                        "undecidable": "[parameters('random')]"
                      }
                    }
                  }
                }
              ]
            }
            """);

        _specParametersFile = WriteFile("spec-parameters.json", """
            {
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "greeting": {
                  "value": "from-spec"
                }
              }
            }
            """);

        // The extension picks the Bicep compiler by the .bicep extension.
        _bicepFile = WriteFile("greeting.bicep", """
            targetScope = 'subscription'

            param greeting string

            output greeting string = greeting
            """);

        // Every parameter is nullable, so a create that passes none of them succeeds.
        _nullableBicepFile = WriteFile("nullable.bicep", """
            targetScope = 'subscription'

            type settings = {
              name: string
            }

            param yourName string?
            param config settings?

            output greeting string = 'Hello ${yourName ?? 'nobody'}'
            output configName string = config.?name ?? 'none'
            """);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    // T1: verifies "T1 Create validates the deployment before the PUT".
    // T2: verifies "T2 Invalid template fails validation without a PUT".
    [Fact]
    public void ValidateWhatIfCreateShowListExportCancelDeleteSubscriptionDeployment()
    {
        // Trims every deployment list to this run's deployments. It runs before the run-scoped-name
        // sanitizer, so it still sees the record run's suffix.
        var mode = RecorderModeResolver.FromEnvironment();
        var deploymentList = new DeploymentListSanitizer(
            [.. new[] { DeploymentName, SlowDeploymentName, ParamsDeploymentName }.Select(name => TestRun.ScopedName(name, mode))]);
        var sanitizers = new RecordingSanitizers(
            [deploymentList, .. RecordingSanitizers.Default.Sanitizers],
            [.. RecordingSanitizers.Default.RemovedHeaders])
            .With([s_bicepGeneratorSanitizer]);

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

    // Without --name the command names the deployment after the template file or URI. These cases
    // return a canned ARM response instead of calling Azure. A null expected name means a GUID.
    [Theory]
    [InlineData(ClientType.CLI, "main.json", null, "main")]
    [InlineData(ClientType.PowerShell, "main.json", null, "main")]
    [InlineData(ClientType.CLI, null, "https://example.com/templates/azuredeploy.json", "azuredeploy")]
    [InlineData(ClientType.PowerShell, null, "https://example.com/templates/azuredeploy.json", "azuredeploy")]
    [InlineData(ClientType.CLI, "bad name!.json", null, null)]
    [InlineData(ClientType.PowerShell, "bad name!.json", null, null)]
    public void ValidateWithoutName_NamesDeploymentAfterTemplate(ClientType surface, string? templateFileName, string? templateUri, string? expectedName)
    {
        string? templateFile = null;
        if (templateFileName is not null)
        {
            templateFile = Path.Combine(_directory, templateFileName);
            File.Copy(_templateFile, templateFile);
        }

        var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$$"""{"properties":{"provisioningState":"{{{State.Succeeded}}}"}}""", Encoding.UTF8, "application/json"),
        });
        using (var host = new ScenarioHost(surface, http, extensions: s_extensions))
        {
            if (surface == ClientType.CLI)
            {
                string[] template = templateFile is not null
                    ? ["--template-file", templateFile, "--parameter", $"greeting={Greeting}"]
                    : ["--template-uri", templateUri!, "--query-string", "sig=abc"];
                host.RunCli(["deployment", "sub", "validate", "--subscription-id", SubscriptionId, "--location", Location, .. template]);
            }
            else
            {
                var parameters = new Dictionary<string, object>
                {
                    ["SubscriptionId"] = SubscriptionId,
                    ["Location"] = Location,
                };
                if (templateFile is not null)
                {
                    parameters["TemplateFile"] = templateFile;
                    parameters["Parameter"] = new[] { $"greeting={Greeting}" };
                }
                else
                {
                    parameters["TemplateUri"] = templateUri!;
                    parameters["QueryString"] = "sig=abc";
                }
                host.RunPowerShell("Test-AzDeployment", parameters);
            }
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

    // Record mode only. Deletes what the run created; a step that fails, such as deleting a deployment
    // the workflow never reached, doesn't stop the others.
    private static void CleanUp(ScenarioHost host, string subscriptionId)
    {
        string[] deployments =
        [
            DeploymentName, SlowDeploymentName, FailedDeploymentName, CreateFailedDeploymentName, ParamsDeploymentName,
            WhatIfSetupName, SpecDeploymentName, BicepDeploymentName, BicepNullableDeploymentName,
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
        foreach (var group in new[] { SlowResourceGroupName, WhatIfGroupName, SpecGroupName })
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
    private void RunCliWorkflow(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        RunCliLifecycle(host, subscriptionId, mode);
        RunCliInFlightCancel(host, subscriptionId, mode);
        RunCliFailures(host, subscriptionId, mode);
        RunCliParameters(host, subscriptionId, mode);
        RunCliWhatIf(host, subscriptionId, mode);
        RunCliTemplateSpec(host, subscriptionId, mode);

        // The extension compiles Bicep locally before it sends the request, so playback skips these
        // steps on a machine without the Bicep CLI.
        if (mode == RecorderMode.Record || BicepCliInstalled())
        {
            RunCliBicep(host, subscriptionId, mode);
        }
    }

    private void RunCliLifecycle(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(DeploymentName, mode);

        AssertValidated(host.RunCli(
            "deployment", "sub", "validate",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _templateFile,
            "--parameter", $"greeting={Greeting}"));

        AssertNoWhatIfChanges(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _templateFile,
            "--parameter", $"greeting={Greeting}"));

        AssertDeployedWithGreeting(name, host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _templateFile,
            "--parameter", $"greeting={Greeting}"));

        Assert.Empty(host.RunCli(
            "deployment", "sub", "wait",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--exists", "--deleted",
            "--interval", "1"));

        AssertDeployedWithGreeting(name, host.RunCli(
            "deployment", "sub", "show",
            "--subscription-id", subscriptionId,
            "--name", name));

        Assert.Contains(name, DeploymentNames(host.RunCli(
            "deployment", "sub", "list",
            "--subscription-id", subscriptionId)));

        var exported = Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
            "deployment", "sub", "export",
            "--subscription-id", subscriptionId,
            "--name", name)));
        Assert.Contains("subscriptionDeploymentTemplate", exported.GetProperty("$schema").GetString());
        Assert.True(exported.GetProperty("outputs").TryGetProperty("greeting", out _), $"Exported template has no greeting output: {exported}");

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

        Assert.Empty(host.RunCli(
            "deployment", "sub", "wait",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--updated", "--deleted",
            "--interval", "1"));

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

    private void RunCliFailures(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var missingName = TestRun.ScopedName(MissingDeploymentName, mode);

        // These throw before the command sends any request. Playback fails on an unrecorded request,
        // so a passing run also proves nothing reached ARM.
        var noTemplate = Assert.ThrowsAny<ArgumentException>(() => host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", missingName,
            "--location", Location));
        Assert.Contains("Exactly one of TemplateUri", noTemplate.Message);

        var noCondition = Assert.Throws<InvalidInputException>(() => host.RunCli(
            "deployment", "sub", "wait",
            "--subscription-id", subscriptionId,
            "--name", missingName));
        Assert.Contains("Specify at least one of --created, --updated, --deleted, or --exists.", noCondition.Message);

        Assert.Throws<TimeoutException>(() => host.RunCli(
            "deployment", "sub", "wait",
            "--subscription-id", subscriptionId,
            "--name", missingName,
            "--exists",
            "--timeout", "0"));

        // A deployment that ARM accepts and then fails reports the ARM error. Playback fails on any
        // unrecorded request, so a passing run also proves the deployment GET and the operations list
        // were sent.
        var createFailedName = TestRun.ScopedName(CreateFailedDeploymentName, mode);
        AssertCreateFailed(createFailedName, Assert.Throws<InvalidOperationException>(() => host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", createFailedName,
            "--location", Location,
            "--template-file", _failingTemplateFile)));

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", createFailedName));

        // T2, spec scenario "T2 Invalid template fails validation without a PUT". Nothing is created,
        // so there is no cleanup; AssertRequestBodies checks that no PUT was recorded.
        AssertCreateFailedValidation(Assert.Throws<InvalidOperationException>(() => host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(CreateInvalidDeploymentName, mode),
            "--location", Location,
            "--template-file", _invalidTemplateFile)));

        // The recording has one 404 for the missing deployment; the wait stops when the timeout runs
        // out instead of sleeping the full 30-second interval.
        var stopwatch = Stopwatch.StartNew();
        Assert.Throws<TimeoutException>(() => host.RunCli(
            "deployment", "sub", "wait",
            "--subscription-id", subscriptionId,
            "--name", missingName,
            "--created",
            "--timeout", "1",
            "--interval", "30"));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Wait took {stopwatch.Elapsed}.");

        // ARM accepts this deployment and fails it later, so the wait sees it reach Failed.
        var failedName = TestRun.ScopedName(FailedDeploymentName, mode);
        Assert.Equal(201, RestStatusCode(host.RunCli(
            "rest",
            "--method", "PUT",
            "--uri", DeploymentUri(subscriptionId, failedName),
            "--body", FailedDeploymentBody.Replace("__LOCATION__", Location))));

        var failed = Assert.Throws<InvalidOperationException>(() => host.RunCli(
            "deployment", "sub", "wait",
            "--subscription-id", subscriptionId,
            "--name", failedName,
            "--created",
            "--interval", "1"));
        Assert.Equal($"Deployment '{failedName}' failed.", failed.Message);

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", failedName));

        // The what-if POST returns 202, and the polled result reports the invalid template.
        var whatIfFailed = Assert.Throws<InvalidOperationException>(() => host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(InvalidWhatIfName, mode),
            "--location", Location,
            "--template-file", _invalidTemplateFile));
        Assert.Contains("What-if operation failed.", whatIfFailed.Message);
        Assert.Contains("InvalidTemplate", whatIfFailed.Message);
        Assert.Contains("undeclared", whatIfFailed.Message);
    }

    // The JSONC parameters file is overlaid with an inline greeting, and each step sends a different
    // validation level. The inline greeting wins, and the file supplies nestedRGName.
    private void RunCliParameters(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(ParamsDeploymentName, mode);

        var validation = AssertValidated(host.RunCli(
            "deployment", "sub", "validate",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _paramsTemplateFile,
            "--parameters-file", _parametersFile,
            "--parameter", $"greeting={OverlayGreeting}",
            "--validation-level", "Template"));
        Assert.Equal(OverlayGreeting, validation.Properties?.Parameters?["greeting"].Value);
        Assert.Equal(FileGroupName, validation.Properties?.Parameters?["nestedRGName"].Value);

        AssertNoWhatIfChanges(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _paramsTemplateFile,
            "--parameters-file", _parametersFile,
            "--parameter", $"greeting={OverlayGreeting}",
            "--validation-level", "ProviderNoRbac"));

        AssertMergedOutputs(name, host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _paramsTemplateFile,
            "--parameters-file", _parametersFile,
            "--parameter", $"greeting={OverlayGreeting}",
            "--validation-level", "Provider"));

        var shown = AssertMergedOutputs(name, host.RunCli(
            "deployment", "sub", "show",
            "--subscription-id", subscriptionId,
            "--name", name));
        Assert.Equal(DeploymentId(subscriptionId, name), shown.Id, ignoreCase: true);

        // Every listed deployment matches the filter, and this run's deployment is among them.
        var succeeded = Assert.IsType<List<DeploymentExtended>>(Assert.Single(host.RunCli(
            "deployment", "sub", "list",
            "--subscription-id", subscriptionId,
            "--filter", "provisioningState eq 'Succeeded'")));
        Assert.Contains(name, succeeded.Select(d => d.Name));
        Assert.All(succeeded, d => Assert.Equal(State.Succeeded, d.ProvisioningState));

        // A remote parameters file is sent as a link, and ARM resolves the parameter from it.
        var fromUri = AssertValidated(host.RunCli(
            "deployment", "sub", "validate",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(ParamsUriDeploymentName, mode),
            "--location", Location,
            "--template-file", _paramsUriTemplateFile,
            "--parameters-uri", ParametersUri));
        Assert.Equal(ParametersUriGroupName, fromUri.Properties?.Parameters?["nestedRGName"].Value);

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", name));
    }

    // A setup deployment creates a resource group tagged a=one, which the what-if calls compare against.
    private void RunCliWhatIf(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var group = TestRun.ScopedName(WhatIfGroupName, mode);
        var newGroup = TestRun.ScopedName(WhatIfNewGroupName, mode);
        var setupName = TestRun.ScopedName(WhatIfSetupName, mode);

        var setup = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", setupName,
            "--location", Location,
            "--template-file", _taggedGroupTemplateFile,
            "--parameter", $"groupName={group}",
            "--parameter", "tagValue=one")));
        Assert.Equal(State.Succeeded, setup.ProvisioningState);

        // Changing the tag is a Modify with a delta from the old value to the new one.
        var modify = Assert.Single(AssertWhatIfSucceeded(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(WhatIfModifyName, mode),
            "--location", Location,
            "--template-file", _taggedGroupTemplateFile,
            "--parameter", $"groupName={group}",
            "--parameter", "tagValue=two")).Properties?.Changes ?? []);
        Assert.Equal("Modify", modify.ChangeType);
        Assert.EndsWith($"/resourceGroups/{group}", modify.ResourceId, StringComparison.OrdinalIgnoreCase);
        AssertTagDelta(modify, "one", "two");

        // The conditional group is a potential change, the group whose name what-if can't calculate
        // is unsupported, and the nested deployment is short-circuited.
        var shortCircuit = AssertWhatIfSucceeded(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(WhatIfShortCircuitName, mode),
            "--location", Location,
            "--template-file", _shortCircuitTemplateFile,
            "--parameter", $"groupName={group}"));

        var unsupported = Assert.Single(shortCircuit.Properties?.Changes ?? [], c => c.ChangeType == "Unsupported");
        Assert.False(string.IsNullOrWhiteSpace(unsupported.UnsupportedReason));

        var potential = Assert.Single(
            shortCircuit.Properties?.PotentialChanges ?? [],
            c => c.ResourceId?.EndsWith($"/resourceGroups/{group}", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Equal("Modify", potential.ChangeType);
        AssertTagDelta(potential, "one", "three");

        Assert.NotNull(shortCircuit.Properties?.Diagnostics);
        var diagnostics = JsonSerializer.SerializeToElement(shortCircuit.Properties.Diagnostics);
        Assert.Contains(
            diagnostics.EnumerateArray(),
            d => d.GetProperty("code").GetString() == "NestedDeploymentShortCircuited"
                 && d.GetProperty("level").GetString() == "Warning"
                 && d.GetProperty("target").GetString()!.EndsWith(
                     $"/providers/Microsoft.Resources/deployments/{WhatIfNestedDeploymentName}", StringComparison.OrdinalIgnoreCase));

        // A group that doesn't exist yet is a Create. FullResourcePayloads, the default, includes the
        // resource payload; ResourceIdOnly includes only the id.
        var full = Assert.Single(AssertWhatIfSucceeded(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(WhatIfCreateName, mode),
            "--location", Location,
            "--template-file", _taggedGroupTemplateFile,
            "--parameter", $"groupName={newGroup}",
            "--parameter", "tagValue=one")).Properties?.Changes ?? []);
        Assert.Equal("Create", full.ChangeType);
        Assert.EndsWith($"/resourceGroups/{newGroup}", full.ResourceId, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(full.After);
        Assert.Null(full.Before);

        var idOnly = Assert.Single(AssertWhatIfSucceeded(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(WhatIfResourceIdOnlyName, mode),
            "--location", Location,
            "--template-file", _taggedGroupTemplateFile,
            "--parameter", $"groupName={newGroup}",
            "--parameter", "tagValue=one",
            "--result-format", "ResourceIdOnly")).Properties?.Changes ?? []);
        Assert.Equal("Create", idOnly.ChangeType);
        Assert.EndsWith($"/resourceGroups/{newGroup}", idOnly.ResourceId, StringComparison.OrdinalIgnoreCase);
        Assert.Null(idOnly.Before);
        Assert.Null(idOnly.After);
        Assert.Null(idOnly.Delta);

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", setupName));
    }

    // The resource group holds the template spec. No command creates a spec, so raw PUTs create the
    // spec and its v1 version. The last create references a v2 that doesn't exist.
    private void RunCliTemplateSpec(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(SpecDeploymentName, mode);
        var group = TestRun.ScopedName(SpecGroupName, mode);
        var specUrl = SpecUrl(subscriptionId, group);
        var specId = SpecVersionId(subscriptionId, group, "v1");

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

        AssertValidated(host.RunCli(
            "deployment", "sub", "validate",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-spec-id", specId,
            "--parameters-file", _specParametersFile));

        AssertNoWhatIfChanges(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-spec-id", specId,
            "--parameters-file", _specParametersFile));

        AssertDeployedFromSpec(name, specId, host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-spec-id", specId,
            "--parameters-file", _specParametersFile));

        AssertDeployedFromSpec(name, specId, host.RunCli(
            "deployment", "sub", "show",
            "--subscription-id", subscriptionId,
            "--name", name));

        var exported = Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
            "deployment", "sub", "export",
            "--subscription-id", subscriptionId,
            "--name", name)));
        Assert.Equal(JsonValueKind.Object, exported.ValueKind);
        Assert.True(exported.GetProperty("outputs").TryGetProperty("greeting", out _), $"Exported template has no greeting output: {exported}");

        var missingVersion = Assert.ThrowsAny<Exception>(() => host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", TestRun.ScopedName(SpecMissingVersionName, mode),
            "--location", Location,
            "--template-spec-id", SpecVersionId(subscriptionId, group, "v2"),
            "--parameters-file", _specParametersFile));
        Assert.Contains("InvalidTemplateSpec", missingVersion.ToString());

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", name));
    }

    // A Bicep file compiles locally, then validates, runs what-if, and deploys. Every parameter of
    // the nullable file is optional, so its create passes none and the outputs fall back to defaults.
    private void RunCliBicep(ScenarioHost host, string subscriptionId, RecorderMode mode)
    {
        var name = TestRun.ScopedName(BicepDeploymentName, mode);
        var nullableName = TestRun.ScopedName(BicepNullableDeploymentName, mode);

        AssertValidated(host.RunCli(
            "deployment", "sub", "validate",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _bicepFile,
            "--parameter", $"greeting={BicepGreeting}"));

        AssertNoWhatIfChanges(host.RunCli(
            "deployment", "sub", "what-if",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _bicepFile,
            "--parameter", $"greeting={BicepGreeting}"));

        AssertDeployedFromBicep(name, host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", name,
            "--location", Location,
            "--template-file", _bicepFile,
            "--parameter", $"greeting={BicepGreeting}"));

        AssertDeployedWithNullDefaults(nullableName, host.RunCli(
            "deployment", "sub", "create",
            "--subscription-id", subscriptionId,
            "--name", nullableName,
            "--location", Location,
            "--template-file", _nullableBicepFile));

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", name));

        Assert.Empty(host.RunCli(
            "deployment", "sub", "delete",
            "--subscription-id", subscriptionId,
            "--name", nullableName));
    }

    // PowerShell runs in playback only, so names are the base names and ids carry the placeholder
    // subscription. It has no subscription what-if, wait, or export, so it replays the rest of the
    // CLI's requests in the same order.
    private void RunPowerShellWorkflow(ScenarioHost host)
    {
        RunPowerShellLifecycle(host);
        RunPowerShellInFlightCancel(host);
        RunPowerShellFailures(host);
        RunPowerShellParameters(host);
        RunPowerShellTemplateSpec(host);

        if (BicepCliInstalled())
        {
            RunPowerShellBicep(host);
        }
    }

    private void RunPowerShellLifecycle(ScenarioHost host)
    {
        AssertValidated(host.RunPowerShell("Test-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _templateFile,
            ["Parameter"] = new[] { $"greeting={Greeting}" },
        }));

        AssertDeployedWithGreeting(DeploymentName, host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _templateFile,
            ["Parameter"] = new[] { $"greeting={Greeting}" },
        }));

        AssertDeployedWithGreeting(DeploymentName, host.RunPowerShell("Get-AzDeployment_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = DeploymentName,
        }));

        Assert.Contains(DeploymentName, DeploymentNames(host.RunPowerShell("Get-AzDeployment_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
        })));

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

    private void RunPowerShellFailures(ScenarioHost host)
    {
        // These throw before the cmdlet sends any request.
        var noTemplate = Assert.ThrowsAny<ArgumentException>(() => host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = MissingDeploymentName,
            ["Location"] = Location,
        }));
        Assert.Contains("Exactly one of TemplateUri", noTemplate.Message);

        // Only PowerShell can show a deployment by id, or stop and remove a piped deployment, whose Id
        // it parses. A resource group deployment's id is the wrong scope.
        string groupScopedId = $"/subscriptions/{SubscriptionId}/resourceGroups/group/providers/Microsoft.Resources/deployments/{MissingDeploymentName}";
        var wrongScope = Assert.Throws<InvalidInputException>(() => host.RunPowerShell("Get-AzDeployment_GetById", new Dictionary<string, object>
        {
            ["Id"] = groupScopedId,
        }));
        Assert.Contains("Invalid deployment id", wrongScope.Message);

        var piped = new Dictionary<string, object> { ["InputObject"] = new DeploymentExtended { Id = groupScopedId } };
        var stop = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<StopDeploymentCmdlet>(piped));
        var remove = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<RemoveDeploymentCmdlet>(piped));
        Assert.All(new[] { stop, remove }, error =>
        {
            Assert.Contains("Invalid deployment id", error.Message);
            Assert.Contains("/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}", error.Message);
        });

        AssertCreateFailed(CreateFailedDeploymentName, Assert.Throws<InvalidOperationException>(() => host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = CreateFailedDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _failingTemplateFile,
        })));

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = CreateFailedDeploymentName,
        }));

        AssertCreateFailedValidation(Assert.Throws<InvalidOperationException>(() => host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = CreateInvalidDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _invalidTemplateFile,
        })));
    }

    // The same merge of the parameters file and the inline greeting as the CLI, shown by resource id
    // instead of by name. PowerShell has no list filter.
    private void RunPowerShellParameters(ScenarioHost host)
    {
        var validation = AssertValidated(host.RunPowerShell("Test-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ParamsDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _paramsTemplateFile,
            ["ParametersFile"] = _parametersFile,
            ["Parameter"] = new[] { $"greeting={OverlayGreeting}" },
            ["ValidationLevel"] = "Template",
        }));
        Assert.Equal(OverlayGreeting, validation.Properties?.Parameters?["greeting"].Value);
        Assert.Equal(FileGroupName, validation.Properties?.Parameters?["nestedRGName"].Value);

        AssertMergedOutputs(ParamsDeploymentName, host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ParamsDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _paramsTemplateFile,
            ["ParametersFile"] = _parametersFile,
            ["Parameter"] = new[] { $"greeting={OverlayGreeting}" },
            ["ValidationLevel"] = "Provider",
        }));

        // The same GET as the CLI's show, resolved from the deployment's resource id.
        string id = DeploymentId(SubscriptionId, ParamsDeploymentName);
        var shown = AssertMergedOutputs(ParamsDeploymentName, host.RunPowerShell("Get-AzDeployment_GetById", new Dictionary<string, object>
        {
            ["Id"] = id,
        }));
        Assert.Equal(id, shown.Id, ignoreCase: true);

        var fromUri = AssertValidated(host.RunPowerShell("Test-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ParamsUriDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _paramsUriTemplateFile,
            ["ParametersUri"] = ParametersUri,
        }));
        Assert.Equal(ParametersUriGroupName, fromUri.Properties?.Parameters?["nestedRGName"].Value);

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ParamsDeploymentName,
        }));
    }

    private void RunPowerShellTemplateSpec(ScenarioHost host)
    {
        string specUrl = SpecUrl(SubscriptionId, SpecGroupName);
        string specId = SpecVersionId(SubscriptionId, SpecGroupName, "v1");

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

        AssertValidated(host.RunPowerShell("Test-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecDeploymentName,
            ["Location"] = Location,
            ["TemplateSpecId"] = specId,
            ["ParametersFile"] = _specParametersFile,
        }));

        AssertDeployedFromSpec(SpecDeploymentName, specId, host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecDeploymentName,
            ["Location"] = Location,
            ["TemplateSpecId"] = specId,
            ["ParametersFile"] = _specParametersFile,
        }));

        AssertDeployedFromSpec(SpecDeploymentName, specId, host.RunPowerShell("Get-AzDeployment_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecDeploymentName,
        }));

        var missingVersion = Assert.ThrowsAny<Exception>(() => host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecMissingVersionName,
            ["Location"] = Location,
            ["TemplateSpecId"] = SpecVersionId(SubscriptionId, SpecGroupName, "v2"),
            ["ParametersFile"] = _specParametersFile,
        }));
        Assert.Contains("InvalidTemplateSpec", missingVersion.ToString());

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = SpecDeploymentName,
        }));
    }

    private void RunPowerShellBicep(ScenarioHost host)
    {
        AssertValidated(host.RunPowerShell("Test-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = BicepDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _bicepFile,
            ["Parameter"] = new[] { $"greeting={BicepGreeting}" },
        }));

        AssertDeployedFromBicep(BicepDeploymentName, host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = BicepDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _bicepFile,
            ["Parameter"] = new[] { $"greeting={BicepGreeting}" },
        }));

        AssertDeployedWithNullDefaults(BicepNullableDeploymentName, host.RunPowerShell("New-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = BicepNullableDeploymentName,
            ["Location"] = Location,
            ["TemplateFile"] = _nullableBicepFile,
        }));

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = BicepDeploymentName,
        }));

        Assert.Empty(host.RunPowerShell("Remove-AzDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = BicepNullableDeploymentName,
        }));
    }

    private static DeploymentValidateResult AssertValidated(IReadOnlyList<object> outputs)
    {
        var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(outputs));
        Assert.Null(validation.Error);
        Assert.Equal(State.Succeeded, validation.Properties?.ProvisioningState);
        return validation;
    }

    private static WhatIfOperationResult AssertWhatIfSucceeded(IReadOnlyList<object> outputs)
    {
        var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(outputs));
        Assert.Equal(State.Succeeded, whatIf.Status);
        Assert.Null(whatIf.Error);
        return whatIf;
    }

    private static void AssertNoWhatIfChanges(IReadOnlyList<object> outputs) =>
        Assert.Empty(AssertWhatIfSucceeded(outputs).Properties?.Changes ?? []);

    private static DeploymentExtended AssertDeployed(string name, IReadOnlyList<object> outputs)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(outputs));
        Assert.Equal(name, deployment.Name);
        Assert.Equal(State.Succeeded, deployment.ProvisioningState);
        return deployment;
    }

    private static void AssertDeployedWithGreeting(string name, IReadOnlyList<object> outputs) =>
        Assert.Equal(Greeting, AssertDeployed(name, outputs).Properties?.Outputs?["greeting"].Value);

    /// <summary>Asserts the outputs carry the inline greeting and the parameters file's group name.</summary>
    private static DeploymentExtended AssertMergedOutputs(string name, IReadOnlyList<object> outputs)
    {
        var deployment = AssertDeployed(name, outputs);
        Assert.Equal(OverlayGreeting, deployment.Properties?.Outputs?["greeting"].Value);
        Assert.Equal(FileGroupName, deployment.Properties?.Outputs?["nestedRGName"].Value);
        return deployment;
    }

    /// <summary>Asserts the deployment used the spec's parameters and links back to the spec version.</summary>
    private static void AssertDeployedFromSpec(string name, string specId, IReadOnlyList<object> outputs)
    {
        var deployment = AssertDeployed(name, outputs);
        Assert.Equal(SpecGreeting, deployment.Properties?.Outputs?["greeting"].Value);
        Assert.Equal(specId, deployment.Properties?.TemplateLink?.Id, ignoreCase: true);
    }

    private static void AssertDeployedFromBicep(string name, IReadOnlyList<object> outputs) =>
        Assert.Equal(BicepGreeting, AssertDeployed(name, outputs).Properties?.Outputs?["greeting"].Value);

    private static void AssertDeployedWithNullDefaults(string name, IReadOnlyList<object> outputs)
    {
        var deployment = AssertDeployed(name, outputs);
        Assert.Equal("Hello nobody", deployment.Properties?.Outputs?["greeting"].Value);
        Assert.Equal("none", deployment.Properties?.Outputs?["configName"].Value);
    }

    private static void AssertCreateFailed(string name, InvalidOperationException error)
    {
        Assert.StartsWith($"Deployment '{name}' failed.", error.Message);
        Assert.Contains("DeploymentFailed", error.Message);
        Assert.Contains("ResourceGroupNotFound", error.Message);
        // ARM's DeploymentFailed message carries a correlation id too, so check the appended last line.
        Assert.Matches(@"^Correlation ID: \S+$", error.Message.Split('\n')[^1].Trim());
        Assert.Contains("Long running operation failed", error.InnerException?.Message);
    }

    private static void AssertCreateFailedValidation(InvalidOperationException error)
    {
        Assert.StartsWith("Deployment validation failed.", error.Message);
        Assert.Contains("InvalidTemplate", error.Message);
    }

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

    /// <summary>
    /// ARM may report the tag as one <c>tags.a</c> entry or as a <c>tags</c> entry with an <c>a</c> child, so the
    /// delta is flattened to full paths before the lookup.
    /// </summary>
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

    private string WriteFile(string fileName, string content)
    {
        string path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Checks the values inside the recorded requests. The run-scoped-name sanitizer strips the record run's
    /// suffix from every name, so the bodies carry the base names.
    /// </summary>
    private static void AssertRequestBodies(SessionRecord record)
    {
        AssertLifecycleRequestBodies(record);
        AssertCreateValidatesBeforePut(record);
        AssertParametersRequestBodies(record);
        AssertWhatIfRequestBodies(record);
        AssertTemplateSpecRequestBodies(record);
        AssertBicepRequestBodies(record);
    }

    private static void AssertLifecycleRequestBodies(SessionRecord record)
    {
        foreach (var body in ValidateWhatIfCreateBodies(record, DeploymentName))
        {
            Assert.Equal(Location, body.GetProperty("location").GetString());
            var properties = body.GetProperty("properties");
            Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
            Assert.Contains("subscriptionDeploymentTemplate", properties.GetProperty("template").GetProperty("$schema").GetString());
            Assert.Equal(Greeting, properties.GetProperty("parameters").GetProperty("greeting").GetProperty("value").GetString());
        }

        var slow = Assert.Single(Requests(record, "PUT", $"/deployments/{SlowDeploymentName}?"));
        Assert.Equal(
            SlowResourceGroupName,
            slow.GetProperty("properties").GetProperty("parameters").GetProperty("groupName").GetProperty("value").GetString());

        // A failed create shows the deployment and lists its operations to report the ARM error.
        string createFailed = $"/deployments/{CreateFailedDeploymentName}";
        Assert.Single(record.Entries, e => IsRequest(e, "GET", $"{createFailed}?"));
        Assert.Single(record.Entries, e => IsRequest(e, "GET", $"{createFailed}/operations?"));
    }

    /// <summary>
    /// T1, spec scenario "T1 Create validates the deployment before the PUT": the standalone validate comes
    /// first, then the create's validate, then its PUT with the same body. T2's invalid template fails that
    /// validate, so no PUT is sent.
    /// </summary>
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

    private static void AssertParametersRequestBodies(SessionRecord record)
    {
        // Validate, what-if, and create each passed their own validation level.
        string[] levels = ["Template", "ProviderNoRbac", "Provider"];
        foreach (var (level, body) in levels.Zip(ValidateWhatIfCreateBodies(record, ParamsDeploymentName)))
        {
            Assert.Equal(Location, body.GetProperty("location").GetString());
            var properties = body.GetProperty("properties");
            Assert.Equal(level, properties.GetProperty("validationLevel").GetString());
            var parameters = properties.GetProperty("parameters");
            Assert.Equal(OverlayGreeting, parameters.GetProperty("greeting").GetProperty("value").GetString());
            Assert.Equal(FileGroupName, parameters.GetProperty("nestedRGName").GetProperty("value").GetString());
            Assert.False(properties.TryGetProperty("parametersLink", out _));
        }

        var uriProperties = Assert.Single(Requests(record, "POST", $"/deployments/{ParamsUriDeploymentName}/validate")).GetProperty("properties");
        Assert.Equal(ParametersUri, uriProperties.GetProperty("parametersLink").GetProperty("uri").GetString());
        Assert.False(uriProperties.TryGetProperty("parameters", out _));
        Assert.False(uriProperties.TryGetProperty("validationLevel", out _));

        // The lifecycle's unfiltered lists share this URI, so check that exactly one list sent the filter.
        Assert.Single(record.Entries, e => IsRequest(e, "GET", "/providers/Microsoft.Resources/deployments?")
            && Uri.UnescapeDataString(e.Request.Uri).Contains("filter=provisioningState", StringComparison.Ordinal));
    }

    private static void AssertWhatIfRequestBodies(SessionRecord record)
    {
        var expected = new Dictionary<string, (string Group, string Tag, string Format)>
        {
            [WhatIfModifyName] = (WhatIfGroupName, "two", "FullResourcePayloads"),
            [WhatIfCreateName] = (WhatIfNewGroupName, "one", "FullResourcePayloads"),
            [WhatIfResourceIdOnlyName] = (WhatIfNewGroupName, "one", "ResourceIdOnly"),
        };
        foreach (var (name, (group, tag, format)) in expected)
        {
            var properties = Assert.Single(Requests(record, "POST", $"/deployments/{name}/whatIf")).GetProperty("properties");
            Assert.Equal(format, properties.GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
            var parameters = properties.GetProperty("parameters");
            Assert.Equal(group, parameters.GetProperty("groupName").GetProperty("value").GetString());
            Assert.Equal(tag, parameters.GetProperty("tagValue").GetProperty("value").GetString());
        }

        var shortCircuit = Assert.Single(Requests(record, "POST", $"/deployments/{WhatIfShortCircuitName}/whatIf")).GetProperty("properties");
        Assert.Equal(WhatIfGroupName, shortCircuit.GetProperty("parameters").GetProperty("groupName").GetProperty("value").GetString());
        Assert.False(shortCircuit.GetProperty("parameters").TryGetProperty("random", out _));
    }

    private static void AssertTemplateSpecRequestBodies(SessionRecord record)
    {
        foreach (var body in ValidateWhatIfCreateBodies(record, SpecDeploymentName))
        {
            AssertSpecBody(body, "v1");
        }

        // The missing version fails in the create's validate POST, so no PUT is sent.
        AssertSpecBody(Assert.Single(Requests(record, "POST", $"/deployments/{SpecMissingVersionName}/validate")), "v2");
        Assert.Empty(Requests(record, "PUT", $"/deployments/{SpecMissingVersionName}?"));
    }

    private static void AssertSpecBody(JsonElement body, string version)
    {
        Assert.Equal(Location, body.GetProperty("location").GetString());
        var properties = body.GetProperty("properties");
        Assert.False(properties.TryGetProperty("template", out _));
        Assert.EndsWith(
            $"/resourceGroups/{SpecGroupName}/providers/Microsoft.Resources/templateSpecs/{SpecName}/versions/{version}",
            properties.GetProperty("templateLink").GetProperty("id").GetString());
        Assert.Equal(SpecGreeting, properties.GetProperty("parameters").GetProperty("greeting").GetProperty("value").GetString());
    }

    private static void AssertBicepRequestBodies(SessionRecord record)
    {
        foreach (var body in ValidateWhatIfCreateBodies(record, BicepDeploymentName))
        {
            var properties = AssertCompiledTemplate(body);
            Assert.Equal(BicepGreeting, properties.GetProperty("parameters").GetProperty("greeting").GetProperty("value").GetString());
        }

        var nullable = AssertCompiledTemplate(Assert.Single(Requests(record, "PUT", $"/deployments/{BicepNullableDeploymentName}?")));
        if (nullable.TryGetProperty("parameters", out var parameters))
        {
            Assert.False(parameters.TryGetProperty("yourName", out _));
            Assert.False(parameters.TryGetProperty("config", out _));
        }
    }

    /// <summary>Asserts the request carries the compiled ARM JSON inline, never the Bicep source or a link.</summary>
    private static JsonElement AssertCompiledTemplate(JsonElement body)
    {
        Assert.Equal(Location, body.GetProperty("location").GetString());
        var properties = body.GetProperty("properties");
        Assert.False(properties.TryGetProperty("templateLink", out _));
        Assert.Contains("subscriptionDeploymentTemplate", properties.GetProperty("template").GetProperty("$schema").GetString());
        return properties;
    }

    /// <summary>
    /// Returns the bodies of the deployment's standalone validate, what-if, and create PUT, in that order.
    /// Create validates too, so the first validate POST is the standalone one.
    /// </summary>
    private static JsonElement[] ValidateWhatIfCreateBodies(SessionRecord record, string name) =>
    [
        Requests(record, "POST", $"/deployments/{name}/validate")[0],
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

    // Looks for the Bicep CLI on PATH and in the az-bundled location, the same places the extension's
    // compiler looks.
    private static bool BicepCliInstalled()
    {
        string command = OperatingSystem.IsWindows() ? "bicep.exe" : "bicep";
        bool onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir.Trim(), command)));
        return onPath || File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure", "bin", command));
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
