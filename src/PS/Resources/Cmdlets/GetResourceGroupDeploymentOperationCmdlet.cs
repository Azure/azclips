// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzResourceGroupDeploymentOperation", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentOperation")]
public class GetResourceGroupDeploymentOperationCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = "List", Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string? ResourceGroupName { get; set; }

    [Alias("Name")]
    [Parameter(ParameterSetName = "Get", Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = "List", Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string? DeploymentName { get; set; }

    [Alias("OperationId")]
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    public string[]? OperationIds { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourceGroupDeploymentOperationError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DeploymentName)))
        {
            parameters.Add("DeploymentName", DeploymentName!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(OperationIds)))
        {
            parameters.Add("OperationIds", OperationIds!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var commandName = $"Get-AzResourceGroupDeploymentOperation_{ParameterSetName}";
        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
