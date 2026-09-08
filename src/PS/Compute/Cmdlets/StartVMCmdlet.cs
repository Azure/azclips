// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsLifecycle.Start, "AzVM", DefaultParameterSetName = ResourceGroupNameParameterSet, SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(void))]
public class StartVMCmdlet : PSCmdlet
{
    private const string ResourceGroupNameParameterSet = "ResourceGroupNameParameterSet";
    private const string IdParameterSet = "IdParameterSet";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupNameParameterSet)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupNameParameterSet)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = IdParameterSet)]
    public string Id { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VMError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        bool byId = ParameterSetName == IdParameterSet;
        string target = byId ? $"VM '{Id}'" : $"VM '{Name}' in resource group '{ResourceGroupName}'";

        if (!ShouldProcess(target, "Start"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        // The processor addresses VMs by id array; -Id is the single-target PowerShell spelling of it.
        var parameters = byId
            ? new Dictionary<string, object> { { nameof(Id), new[] { Id } } }
            : new Dictionary<string, object>
            {
                { nameof(Name), Name },
                { nameof(ResourceGroupName), ResourceGroupName },
            };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add(nameof(SubscriptionId), SubscriptionId);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add(nameof(Profile), Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", "Start-AzVM", parameters, false, moduleName, moduleVersion);
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
