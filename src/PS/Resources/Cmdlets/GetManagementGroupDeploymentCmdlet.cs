// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzManagementGroupDeployment", DefaultParameterSetName = ListParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended")]
public class GetManagementGroupDeploymentCmdlet : PSCmdlet
{
    private const string GetParameterSet = "Get";
    private const string ListParameterSet = "List";
    private const string IdParameterSet = "Id";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams _streams = null!;
    private IEngine _engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = GetParameterSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = ListParameterSet)]
    public string ManagementGroupId { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = true, ParameterSetName = GetParameterSet)]
    public string? Name { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdParameterSet)]
    public string? Id { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        _streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified, enumerateOutput: true);
        _engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        (string commandName, Dictionary<string, object> parameters) = BuildCommand(
            ParameterSetName,
            ManagementGroupId,
            Name,
            Id,
            MyInvocation.BoundParameters.ContainsKey(nameof(Profile)) ? Profile : null);

        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new(
            "Resources",
            commandName,
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);

        using ISession session = _engine.OpenExecutionSession(invocation, _streams, _cts.Token);
        session.Run();
    }

    internal static (string CommandName, Dictionary<string, object> Parameters) BuildCommand(
        string parameterSetName,
        string managementGroupId,
        string? name,
        string? id,
        string? profile)
    {
        Dictionary<string, object> parameters = new();
        string commandName;
        if (parameterSetName == IdParameterSet)
        {
            (managementGroupId, name) = ManagementGroupDeploymentId.Parse(id!);
            parameters[nameof(ManagementGroupId)] = managementGroupId;
            parameters[nameof(Name)] = name;
            commandName = "Get-AzManagementGroupDeployment_Get";
        }
        else
        {
            parameters[nameof(ManagementGroupId)] = managementGroupId;
            commandName = $"Get-AzManagementGroupDeployment_{parameterSetName}";
            if (name is not null)
            {
                parameters[nameof(Name)] = name;
            }
        }
        if (profile is not null)
        {
            parameters[nameof(Profile)] = profile;
        }

        return (commandName, parameters);
    }

    protected override void StopProcessing() => _cts.Cancel();

    protected override void EndProcessing() => _cts.Dispose();

}
