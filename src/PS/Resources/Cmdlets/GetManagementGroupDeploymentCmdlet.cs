// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzManagementGroupDeployment", DefaultParameterSetName = NameSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment")]
public class GetManagementGroupDeploymentCmdlet : PSCmdlet
{
    private const string NameSet = "GetByName";
    private const string IdSet = "GetById";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? ManagementGroupId { get; set; }

    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = false, ParameterSetName = NameSet)]
    public string? Name { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }

    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)]
    public SwitchParameter Pre { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified,
            enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string managementGroupId;
        string? name;
        if (ParameterSetName == IdSet)
        {
            (managementGroupId, string parsedName) =
                ManagementGroupDeploymentCmdletHelpers.ParseId(Id!);
            name = parsedName;
        }
        else
        {
            managementGroupId = ManagementGroupId!;
            name = Name;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = managementGroupId,
        };
        string command = "Get-AzManagementGroupDeployment_List";
        if (!string.IsNullOrWhiteSpace(name))
        {
            parameters[nameof(Name)] = name;
            command = "Get-AzManagementGroupDeployment_Get";
        }
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }

        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            command,
            parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
