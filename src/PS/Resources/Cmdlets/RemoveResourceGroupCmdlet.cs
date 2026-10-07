// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(
    VerbsCommon.Remove,
    "AzResourceGroup",
    DefaultParameterSetName = ResourceGroupNameParameterSet,
    SupportsShouldProcess = true,
    ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(bool))]
public class RemoveResourceGroupCmdlet : PSCmdlet
{
    private const string ResourceGroupNameParameterSet = "RemoveByResourceGroupName";
    private const string ResourceGroupIdParameterSet = "RemoveByResourceGroupId";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Alias("ResourceGroupName")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ResourceGroupNameParameterSet, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    [Alias("ResourceGroupId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupIdParameterSet, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Id { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public string? ForceDeletionType { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourceGroupError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        bool byId = ParameterSetName == ResourceGroupIdParameterSet;
        string target = byId ? $"resource group '{Id}'" : $"resource group '{Name}'";

        if (!ShouldProcess(target, "Remove"))
        {
            return;
        }

        bool confirmationSpecified = MyInvocation.BoundParameters.ContainsKey("Confirm");
        if (!Force.IsPresent
            && !confirmationSpecified
            && !ShouldContinue(
                $"Deleting {target} permanently deletes all resources it contains.",
                "Confirm resource group deletion"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = byId
            ? new Dictionary<string, object> { [nameof(Id)] = Id }
            : new Dictionary<string, object> { [nameof(Name)] = Name };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForceDeletionType)))
        {
            parameters.Add(nameof(ForceDeletionType), ForceDeletionType!);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add(nameof(SubscriptionId), SubscriptionId!);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add(nameof(Profile), Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation(
            "Resources",
            "Remove-AzResourceGroup",
            parameters,
            false,
            moduleName,
            moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
        WriteObject(true);
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
