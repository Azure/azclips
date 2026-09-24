// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Get, "AzVirtualNetwork", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork")]
public class GetVirtualNetworkCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    // Optional in the List set: omitting it lists every virtual network in the subscription,
    // matching `az network vnet list`.
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    public string? ExpandResource { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VirtualNetworkError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))    { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName))) { parameters.Add("ResourceGroupName", ResourceGroupName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))              { parameters.Add("Name", Name!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ExpandResource)))    { parameters.Add("ExpandResource", ExpandResource!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))           { parameters.Add("Profile", Profile!); }

        var commandName = $"Get-AzVirtualNetwork_{ParameterSetName}";

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Network", commandName, parameters, false, moduleName, moduleVersion);
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
