// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsLifecycle.Stop, "AzVM", DefaultParameterSetName = ResourceGroupNameParameterSet, SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(void))]
public class StopVMCmdlet : PSCmdlet
{
    private const string ResourceGroupNameParameterSet = "ResourceGroupNameParameterSet";
    private const string IdParameterSet = "IdParameterSet";
    private const string ResourceGroupHibernateParameterSet = "ResourceGroupHibernateParameterSet";
    private const string IdHibernateParameterSet = "IdHibernateParameterSet";

    private const string StoppingCaption = "Virtual machine stopping operation";
    private const string StoppingConfirmation = "This cmdlet will stop the specified virtual machine. Do you want to continue?";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ResourceGroupNameParameterSet)]
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ResourceGroupHibernateParameterSet)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ResourceGroupNameParameterSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ResourceGroupHibernateParameterSet)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = IdParameterSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = IdHibernateParameterSet)]
    public string Id { get; set; } = "";

    [Parameter(Mandatory = false, ParameterSetName = ResourceGroupNameParameterSet, HelpMessage = "To keep the VM provisioned.")]
    [Parameter(Mandatory = false, ParameterSetName = IdParameterSet, HelpMessage = "To keep the VM provisioned.")]
    public SwitchParameter StayProvisioned { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = ResourceGroupNameParameterSet, HelpMessage = "To request non-graceful VM shutdown when keeping the VM provisioned.")]
    [Parameter(Mandatory = false, ParameterSetName = IdParameterSet, HelpMessage = "To request non-graceful VM shutdown when keeping the VM provisioned.")]
    public SwitchParameter SkipShutdown { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = ResourceGroupHibernateParameterSet, HelpMessage = "Optional parameter to hibernate a virtual machine.")]
    [Parameter(Mandatory = false, ParameterSetName = IdHibernateParameterSet, HelpMessage = "Optional parameter to hibernate a virtual machine.")]
    public SwitchParameter Hibernate { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = ResourceGroupNameParameterSet, HelpMessage = "Optional parameter to force deallocate a virtual machine during stop operations. Cannot be used with -StayProvisioned, -SkipShutdown, or -Hibernate.")]
    [Parameter(Mandatory = false, ParameterSetName = IdParameterSet, HelpMessage = "Optional parameter to force deallocate a virtual machine during stop operations. Cannot be used with -StayProvisioned, -SkipShutdown, or -Hibernate.")]
    public SwitchParameter ForceDeallocate { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "To force the stopping.")]
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
        bool byId = ParameterSetName is IdParameterSet or IdHibernateParameterSet;
        string target = byId ? $"VM '{Id}'" : $"VM '{Name}' in resource group '{ResourceGroupName}'";

        if (ForceDeallocate.IsPresent && (StayProvisioned.IsPresent || SkipShutdown.IsPresent))
        {
            throw new PSArgumentException("The -ForceDeallocate parameter cannot be used together with -StayProvisioned or -SkipShutdown.");
        }

        if (!ShouldProcess(target, "Stop"))
        {
            return;
        }

        if (!Force.IsPresent && !ShouldContinue(StoppingConfirmation, StoppingCaption))
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

        if (StayProvisioned.IsPresent)
        {
            parameters.Add(nameof(StayProvisioned), true);
        }

        if (SkipShutdown.IsPresent)
        {
            parameters.Add(nameof(SkipShutdown), true);
        }

        if (Hibernate.IsPresent)
        {
            parameters.Add(nameof(Hibernate), true);
        }

        if (ForceDeallocate.IsPresent)
        {
            parameters.Add(nameof(ForceDeallocate), true);
        }

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
        var invocation = new CommandInvocation("Compute", "Stop-AzVM", parameters, false, moduleName, moduleVersion);
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
