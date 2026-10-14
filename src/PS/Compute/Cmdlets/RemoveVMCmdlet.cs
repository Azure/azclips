// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsCommon.Remove, "AzVM", DefaultParameterSetName = ResourceGroupNameParameterSet, SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public class RemoveVMCmdlet : PSCmdlet
{
    private const string ResourceGroupNameParameterSet = "ResourceGroupNameParameterSet";
    private const string IdParameterSet = "IdParameterSet";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Alias("ResourceName", "VMName")]
    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupNameParameterSet)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupNameParameterSet)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = IdParameterSet)]
    public string Id { get; set; } = "";

    [Parameter(Mandatory = false, HelpMessage = "Optional parameter to force delete a VM.")]
    public bool? ForceDeletion { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "To force the removal without a confirmation prompt.")]
    public SwitchParameter Force { get; set; }

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

        if (!ShouldProcess(target, "Remove"))
        {
            return;
        }

        if (!Force.IsPresent
            && !ShouldContinue("This cmdlet will remove the specified virtual machine. Do you want to continue?", "Virtual machine removal operation"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

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

        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForceDeletion)))
        {
            parameters.Add(nameof(ForceDeletion), ForceDeletion!.Value);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add(nameof(Profile), Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", "Remove-AzVM", parameters, false, moduleName, moduleVersion);
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
