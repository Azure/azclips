// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsData.Save, "AzDeploymentTemplate", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = "SaveByDeploymentName")]
[Alias("Save-AzSubscriptionDeploymentTemplate")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeploymentTemplateSaveResult")]
public class SaveDeploymentTemplateCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false, ParameterSetName = "SaveByDeploymentName")]
    public string SubscriptionId { get; set; } = "";

    // No pipeline binding: a piped deployment must route to -InputObject so its Id (with subscription) is used.
    [Alias("DeploymentName")]
    [Parameter(Mandatory = true, ParameterSetName = "SaveByDeploymentName", HelpMessage = "The name of the deployment.")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "SaveByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public DeploymentExtended? InputObject { get; set; }

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? Path { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"deployment '{Name ?? InputObject?.Id}'", "Save template"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        // A relative path is relative to the PowerShell session's current file-system location,
        // which can differ from the process working directory the processor would otherwise see.
        var parameters = new Dictionary<string, object>
        {
            { "Path", ResolveOutputPath() },
        };

        if (Name is not null)
        {
            parameters.Add("Name", Name);
        }
        if (InputObject is not null)
        {
            // The piped deployment's Id carries its own subscription; the profile's subscription may differ.
            if (DeploymentObjectResolver.ParseSubscriptionDeploymentId(InputObject.Id) is not { } deployment)
            {
                WriteError(DeploymentObjectResolver.InvalidSubscriptionDeploymentIdError(InputObject));
                return;
            }
            parameters.Add("SubscriptionId", deployment.SubscriptionId);
            parameters.Add("Name", deployment.Name);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }

        if (Force.IsPresent)
        {
            parameters.Add("Force", true);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Save-AzDeploymentTemplate", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    private string ResolveOutputPath() =>
        MyInvocation.BoundParameters.ContainsKey(nameof(Path))
            ? GetUnresolvedProviderPathFromPSPath(Path)
            : SessionState.Path.CurrentFileSystemLocation.Path;

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
