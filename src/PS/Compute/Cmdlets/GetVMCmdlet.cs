// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsCommon.Get, "AzVM", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine")]
public class GetVMCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "ListLocation", Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter Status { get; set; }

    [Parameter(ParameterSetName = "Id", Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string Id { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter UserData { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VMError", ErrorCategory.NotSpecified, enumerateOutput: true);
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
            parameters.Add("ResourceGroupName", ResourceGroupName);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Location)))
        {
            parameters.Add("Location", Location);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Id)))
        {
            // The processor addresses VMs by id array; -Id is the single-target PowerShell spelling of it.
            parameters.Add(nameof(Id), new[] { Id });
        }
        if (UserData.IsPresent)
        {
            parameters.Add(nameof(UserData), true);
        }
        if (Status.IsPresent)
        {
            parameters.Add(nameof(Status), true);
        }

        var parameterSetName = ParameterSetName;
        var commandName = $"Get-AzVM_{parameterSetName}";
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", commandName, parameters, false, moduleName, moduleVersion);
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
