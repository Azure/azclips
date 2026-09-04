// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

/// <summary>
/// Starts a virtual machine. The target is identified either by name plus resource group or by a
/// complete resource id; the two are declared as separate parameter sets so PowerShell enforces the
/// choice before the processor runs.
/// </summary>
[Cmdlet(VerbsLifecycle.Start, "AzVM", DefaultParameterSetName = ResourceGroupNameParameterSet, SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public class StartVMCmdlet : PSCmdlet
{
    private const string ResourceGroupNameParameterSet = "ResourceGroupNameParameterSetName";
    private const string IdParameterSet = "IdParameterSetName";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = ResourceGroupNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = IdParameterSet, Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = ResourceGroupNameParameterSet, Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = ResourceGroupNameParameterSet, Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = "";

    [Parameter(ParameterSetName = IdParameterSet, Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string Id { get; set; } = "";

    [Parameter(Mandatory = false)]
    public SwitchParameter NoWait { get; set; }

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
        string target = byId ? Id : $"VM '{Name}' in resource group '{ResourceGroupName}'";
        if (!ShouldProcess(target, "Start"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();

        if (byId)
        {
            // PowerShell exposes a single -Id; the processor takes the array that --ids binds to.
            parameters.Add("Id", new[] { Id });
        }
        else
        {
            parameters.Add("Name", Name);
            parameters.Add("ResourceGroupName", ResourceGroupName);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(NoWait)))
        {
            parameters.Add("NoWait", NoWait.IsPresent);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
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
