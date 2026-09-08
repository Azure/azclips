// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsCommon.New, "AzVM", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine")]
public class NewVMCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string VmSize { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdminUsername { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdminPassword { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string NetworkInterfaceName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string VirtualNetworkName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string NetworkSecurityGroupName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? SubnetNames { get; set; }

    [Parameter(Mandatory = false)]
    public string[][]? SubnetAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? AddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleNames { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDirections { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleProtocols { get; set; }

    [Parameter(Mandatory = false)]
    public int[]? RulePriorities { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleSourceAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleSourcePortRanges { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDestinationPortRanges { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDestinationAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleAccesses { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VMError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
            { "VmSize", VmSize },
            { "AdminUsername", AdminUsername },
            { "AdminPassword", AdminPassword },
            { "NetworkInterfaceName", NetworkInterfaceName },
            { "VirtualNetworkName", VirtualNetworkName },
            { "NetworkSecurityGroupName", NetworkSecurityGroupName },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))                { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetNames)))                    { parameters.Add("SubnetNames", SubnetNames!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetAddressPrefixes)))          { parameters.Add("SubnetAddressPrefixes", SubnetAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AddressPrefixes)))                { parameters.Add("AddressPrefixes", AddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleNames)))                      { parameters.Add("RuleNames", RuleNames!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDirections)))                 { parameters.Add("RuleDirections", RuleDirections!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleProtocols)))                  { parameters.Add("RuleProtocols", RuleProtocols!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RulePriorities)))                 { parameters.Add("RulePriorities", RulePriorities!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleSourceAddressPrefixes)))      { parameters.Add("RuleSourceAddressPrefixes", RuleSourceAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleSourcePortRanges)))           { parameters.Add("RuleSourcePortRanges", RuleSourcePortRanges!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDestinationPortRanges)))      { parameters.Add("RuleDestinationPortRanges", RuleDestinationPortRanges!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDestinationAddressPrefixes))) { parameters.Add("RuleDestinationAddressPrefixes", RuleDestinationAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleAccesses)))                   { parameters.Add("RuleAccesses", RuleAccesses!); }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", "New-AzVM", parameters, false, moduleName, moduleVersion);
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
