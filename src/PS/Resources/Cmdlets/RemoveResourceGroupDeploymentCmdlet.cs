// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Remove, "AzResourceGroupDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = ByNameParameterSet)]
[OutputType(typeof(void))]
public class RemoveResourceGroupDeploymentCmdlet : PSCmdlet
{
    private const string ByNameParameterSet = "RemoveByResourceGroupName";
    private const string ByIdParameterSet = "RemoveByResourceGroupDeploymentId";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = ByNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = ByIdParameterSet, Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = ByNameParameterSet, Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter(ParameterSetName = ByNameParameterSet, Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = "";

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(ParameterSetName = ByIdParameterSet, Mandatory = true)]
    public string Id { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourceGroupDeploymentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        bool byId = ParameterSetName == ByIdParameterSet;
        string target = byId ? $"deployment '{Id}'" : $"deployment '{Name}' in resource group '{ResourceGroupName}'";

        if (!ShouldProcess(target, "Delete"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = byId
            ? new Dictionary<string, object> { { nameof(Id), Id } }
            : new Dictionary<string, object>
            {
                { nameof(ResourceGroupName), ResourceGroupName },
                { nameof(Name), Name },
            };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add(nameof(SubscriptionId), SubscriptionId);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Remove-AzResourceGroupDeployment", parameters, false, moduleName, moduleVersion);
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
