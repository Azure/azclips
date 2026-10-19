// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzVirtualNetworkSubnetConfig")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.Subnet")]
public class NewVirtualNetworkSubnetConfigCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? AddressPrefix { get; set; }

    [Parameter(Mandatory = false)]
    public string? NetworkSecurityGroupId { get; set; }

    [Parameter(Mandatory = false)]
    public string? RouteTableId { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("ResourceId")]
    public string? NatGatewayId { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? ServiceEndpoint { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? ServiceEndpointPolicy { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? DelegatedService { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? IpamPoolPrefixAllocation { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Enabled", "Disabled", "NetworkSecurityGroupEnabled", "RouteTableEnabled")]
    public string? PrivateEndpointNetworkPoliciesFlag { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Enabled", "Disabled")]
    public string? PrivateLinkServiceNetworkPoliciesFlag { get; set; }

    [Parameter(Mandatory = false)]
    public bool DefaultOutboundAccess { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VirtualNetworkSubnetConfigError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object> { ["Name"] = Name };
        var bound = MyInvocation.BoundParameters;
        if (bound.ContainsKey(nameof(AddressPrefix))) { parameters.Add("AddressPrefix", AddressPrefix!); }
        if (bound.ContainsKey(nameof(NetworkSecurityGroupId))) { parameters.Add("NetworkSecurityGroupId", NetworkSecurityGroupId!); }
        if (bound.ContainsKey(nameof(RouteTableId))) { parameters.Add("RouteTableId", RouteTableId!); }
        if (bound.ContainsKey(nameof(NatGatewayId))) { parameters.Add("NatGatewayId", NatGatewayId!); }
        if (bound.ContainsKey(nameof(ServiceEndpoint))) { parameters.Add("ServiceEndpoint", ServiceEndpoint!); }
        if (bound.ContainsKey(nameof(ServiceEndpointPolicy))) { parameters.Add("ServiceEndpointPolicy", ServiceEndpointPolicy!); }
        if (bound.ContainsKey(nameof(DelegatedService))) { parameters.Add("DelegatedService", DelegatedService!); }
        if (bound.ContainsKey(nameof(IpamPoolPrefixAllocation))) { parameters.Add("IpamPoolPrefixAllocation", IpamPoolPrefixAllocation!); }
        if (bound.ContainsKey(nameof(PrivateEndpointNetworkPoliciesFlag))) { parameters.Add("PrivateEndpointNetworkPoliciesFlag", PrivateEndpointNetworkPoliciesFlag!); }
        if (bound.ContainsKey(nameof(PrivateLinkServiceNetworkPoliciesFlag))) { parameters.Add("PrivateLinkServiceNetworkPoliciesFlag", PrivateLinkServiceNetworkPoliciesFlag!); }
        if (bound.ContainsKey(nameof(DefaultOutboundAccess))) { parameters.Add("DefaultOutboundAccess", DefaultOutboundAccess); }
        if (bound.ContainsKey(nameof(Profile))) { parameters.Add("Profile", Profile!); }

        var invocation = new CommandInvocation("Network", "New-AzVirtualNetworkSubnetConfig", parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
