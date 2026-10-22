// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzVirtualNetwork", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork")]
public class NewVirtualNetworkCmdlet : PSCmdlet
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

    [Parameter(Mandatory = false)]
    [Alias("AddressPrefixes")]
    public string[]? AddressPrefix { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Subnets")]
    public object[]? Subnet { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? SubnetNames { get; set; }

    [Parameter(Mandatory = false)]
    public string[][]? SubnetAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("DnsServers")]
    public string[]? DnsServer { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableDdosProtection { get; set; }

    [Parameter(Mandatory = false)]
    public string? DdosProtectionPlanId { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableVmProtection { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateRange(4, 30)]
    public int FlowTimeout { get; set; }

    [Parameter(Mandatory = false)]
    public string? BgpCommunity { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableEncryption { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("DropUnencrypted", "AllowUnencrypted")]
    public string? EncryptionEnforcementPolicy { get; set; }

    [Parameter(Mandatory = false)]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Disabled", "Basic")]
    public string? PrivateEndpointVNetPoliciesValue { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? IpAllocation { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? IpamPoolPrefixAllocation { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? SummarizedGatewayPrefix { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VirtualNetworkError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"Virtual network '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
        };

        var bound = MyInvocation.BoundParameters;
        if (bound.ContainsKey(nameof(SubscriptionId)))                   { parameters.Add("SubscriptionId", SubscriptionId); }
        if (bound.ContainsKey(nameof(AddressPrefix)))                    { parameters.Add("AddressPrefix", AddressPrefix!); }
        if (bound.ContainsKey(nameof(Subnet)))                           { parameters.Add("Subnet", Subnet!.Select(ToSubnetInput).ToList()); }
        if (bound.ContainsKey(nameof(SubnetNames)))                      { parameters.Add("SubnetNames", SubnetNames!); }
        if (bound.ContainsKey(nameof(SubnetAddressPrefixes)))            { parameters.Add("SubnetAddressPrefixes", SubnetAddressPrefixes!); }
        if (bound.ContainsKey(nameof(DnsServer)))                        { parameters.Add("DnsServer", DnsServer!); }
        if (bound.ContainsKey(nameof(Tag)))                              { parameters.Add("Tag", ToKeyValuePairs(Tag!)); }
        if (bound.ContainsKey(nameof(EnableDdosProtection)))             { parameters.Add("EnableDdosProtection", EnableDdosProtection.IsPresent); }
        if (bound.ContainsKey(nameof(DdosProtectionPlanId)))             { parameters.Add("DdosProtectionPlanId", DdosProtectionPlanId!); }
        if (bound.ContainsKey(nameof(EnableVmProtection)))               { parameters.Add("EnableVmProtection", EnableVmProtection.IsPresent); }
        if (bound.ContainsKey(nameof(FlowTimeout)))                      { parameters.Add("FlowTimeout", FlowTimeout); }
        if (bound.ContainsKey(nameof(BgpCommunity)))                     { parameters.Add("BgpCommunity", BgpCommunity!); }
        if (bound.ContainsKey(nameof(EnableEncryption)))                 { parameters.Add("EnableEncryption", EnableEncryption.IsPresent); }
        if (bound.ContainsKey(nameof(EncryptionEnforcementPolicy)))      { parameters.Add("EncryptionEnforcementPolicy", EncryptionEnforcementPolicy!); }
        if (bound.ContainsKey(nameof(EdgeZone)))                         { parameters.Add("EdgeZone", EdgeZone!); }
        if (bound.ContainsKey(nameof(PrivateEndpointVNetPoliciesValue))) { parameters.Add("PrivateEndpointVNetPoliciesValue", PrivateEndpointVNetPoliciesValue!); }
        if (bound.ContainsKey(nameof(IpAllocation)))                     { parameters.Add("IpAllocation", IpAllocation!); }
        if (bound.ContainsKey(nameof(IpamPoolPrefixAllocation)))         { parameters.Add("IpamPoolPrefixAllocation", IpamPoolPrefixAllocation!); }
        if (bound.ContainsKey(nameof(SummarizedGatewayPrefix)))          { parameters.Add("SummarizedGatewayPrefix", SummarizedGatewayPrefix!); }
        if (bound.ContainsKey(nameof(Profile)))                          { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Network", "New-AzVirtualNetwork", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    // The processor takes KEY=VALUE tokens on both surfaces; PowerShell users expect hashtables.
    private static string[] ToKeyValuePairs(Hashtable table) =>
        table.Cast<DictionaryEntry>()
            .Select(entry => $"{entry.Key}={entry.Value}")
            .ToArray();

    private static object ToSubnetInput(object subnet) =>
        subnet is Hashtable table ? ToKeyValuePairs(table) : subnet;

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
