// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzTenantDeployment", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended")]
public class GetTenantDeploymentCmdlet : PSCmdlet
{
    private const string ListSet = "List";
    private const string NameSet = "GetByName";
    private const string IdSet = "GetById";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Alias("DeploymentName")]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? Name { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = new();
        string command = "Get-AzTenantDeployment_List";
        if (ParameterSetName != ListSet)
        {
            parameters["Name"] = Name ?? TenantDeploymentCmdletHelpers.NameFromId(Id!);
            command = "Get-AzTenantDeployment_Get";
        }
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }

        TenantDeploymentCmdletHelpers.Run(this, engine, streams, _cts.Token, command, parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
