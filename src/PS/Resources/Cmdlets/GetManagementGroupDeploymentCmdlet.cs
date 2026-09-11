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
        Dictionary<string, object> parameters = new();
        string commandName;
        if (ParameterSetName == IdParameterSet)
        {
            (string managementGroupId, string name) = ParseId(Id!);
            parameters[nameof(ManagementGroupId)] = managementGroupId;
            parameters[nameof(Name)] = name;
            commandName = "Get-AzManagementGroupDeployment_Get";
        }
        else
        {
            parameters[nameof(ManagementGroupId)] = ManagementGroupId;
            commandName = $"Get-AzManagementGroupDeployment_{ParameterSetName}";
            if (Name is not null)
            {
                parameters[nameof(Name)] = Name;
            }
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters[nameof(Profile)] = Profile!;
        }

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

    protected override void StopProcessing() => _cts.Cancel();

    protected override void EndProcessing() => _cts.Dispose();

    private static (string ManagementGroupId, string Name) ParseId(string id)
    {
        string[] segments = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int managementGroups = Array.FindIndex(
            segments,
            segment => segment.Equals("managementGroups", StringComparison.OrdinalIgnoreCase));
        int deployments = Array.FindIndex(
            segments,
            managementGroups + 2,
            segment => segment.Equals("deployments", StringComparison.OrdinalIgnoreCase));
        if (managementGroups < 0 || managementGroups + 1 >= segments.Length
            || deployments < 0 || deployments + 1 >= segments.Length)
        {
            throw new ArgumentException("Id must be a management-group deployment resource ID.", nameof(id));
        }

        return (segments[managementGroups + 1], segments[deployments + 1]);
    }
}
