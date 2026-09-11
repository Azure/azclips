// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Stop, "AzManagementGroupDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(bool))]
public class StopManagementGroupDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams _streams = null!;
    private IEngine _engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Name")]
    public string ManagementGroupId { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = true, ParameterSetName = "Name")]
    public string? Name { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = "Id")]
    public string? Id { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        _streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified);
        _engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string managementGroupId = ManagementGroupId;
        string name = Name ?? "";
        if (ParameterSetName == "Id")
        {
            (managementGroupId, name) = ManagementGroupDeploymentId.Parse(Id!);
        }
        if (!ShouldProcess($"deployment '{name}' at management group '{managementGroupId}'", "Cancel"))
        {
            return;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = managementGroupId,
            [nameof(Name)] = name,
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters[nameof(Profile)] = Profile!;
        }

        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new(
            "Resources",
            "Stop-AzManagementGroupDeployment",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);

        using ISession session = _engine.OpenExecutionSession(invocation, _streams, _cts.Token);
        session.Run();
        if (PassThru)
        {
            WriteObject(true);
        }
    }

    protected override void StopProcessing() => _cts.Cancel();

    protected override void EndProcessing() => _cts.Dispose();
}
