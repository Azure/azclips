// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Stop, "AzSubDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = "StopByDeploymentName")]
[Alias("Stop-AzSubscriptionDeployment")]
[OutputType(typeof(bool))]
public class StopSubDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false, ParameterSetName = "StopByDeploymentName")]
    public string SubscriptionId { get; set; } = "";

    // No pipeline binding: a piped deployment must route to -InputObject so its Id (with subscription) is used.
    [Alias("DeploymentName")]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "StopByDeploymentName", HelpMessage = "The name of the deployment.")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "StopByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public DeploymentExtended? InputObject { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PassThru { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"deployment '{Name ?? InputObject?.Id}'", "Cancel"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Stop-AzSubDeployment", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();

        if (PassThru.IsPresent)
        {
            WriteObject(true);
        }
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
