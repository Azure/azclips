// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Stop, "AzManagementGroupDeployment", DefaultParameterSetName = NameSet, SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class StopManagementGroupDeploymentCmdlet : PSCmdlet
{
    private const string NameSet = "StopByName";
    private const string IdSet = "StopById";
    private const string ObjectSet = "StopByInputObject";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? ManagementGroupId { get; set; }
    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = true, ParameterSetName = NameSet)]
    public string? Name { get; set; }
    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ObjectSet)]
    public PSDeployment? InputObject { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter PassThru { get; set; }
    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)] public SwitchParameter Pre { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        (string managementGroupId, string name) = ResolveTarget();
        if (!ShouldProcess(
            $"management group '{managementGroupId}' deployment '{name}'",
            "Cancel"))
        {
            return;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = managementGroupId,
            [nameof(Name)] = name,
        };
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }
        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "Stop-AzManagementGroupDeployment",
            parameters);
        if (PassThru)
        {
            WriteObject(true);
        }
    }

    private (string ManagementGroupId, string Name) ResolveTarget()
    {
        if (Id is not null)
        {
            return ManagementGroupDeploymentCmdletHelpers.ParseId(Id);
        }
        if (InputObject?.Id is not null)
        {
            return ManagementGroupDeploymentCmdletHelpers.ParseId(InputObject.Id);
        }
        return (ManagementGroupId!, Name!);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
