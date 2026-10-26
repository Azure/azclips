// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzNetworkInterface", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface")]
public class NewNetworkInterfaceCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string SubnetId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? PublicIpAddressId { get; set; }

    [Parameter(Mandatory = false)]
    public string? NetworkSecurityGroupId { get; set; }

    [Parameter(Mandatory = false)]
    public string? PrivateIpAddress { get; set; }

    [Parameter(Mandatory = false)]
    public string? IpConfigurationName { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("DnsServers")]
    public string[]? DnsServer { get; set; }

    [Parameter(Mandatory = false)]
    public string? InternalDnsNameLabel { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableAcceleratedNetworking { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableIPForwarding { get; set; }

    [Parameter(Mandatory = false)]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "NetworkInterfaceError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"Network interface '{Name}' in resource group '{ResourceGroupName}'", "Create"))
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
            { "SubnetId", SubnetId },
        };

        var bound = MyInvocation.BoundParameters;
        if (bound.ContainsKey(nameof(SubscriptionId)))              { parameters.Add("SubscriptionId", SubscriptionId); }
        if (bound.ContainsKey(nameof(PublicIpAddressId)))           { parameters.Add("PublicIpAddressId", PublicIpAddressId!); }
        if (bound.ContainsKey(nameof(NetworkSecurityGroupId)))      { parameters.Add("NetworkSecurityGroupId", NetworkSecurityGroupId!); }
        if (bound.ContainsKey(nameof(PrivateIpAddress)))            { parameters.Add("PrivateIpAddress", PrivateIpAddress!); }
        if (bound.ContainsKey(nameof(IpConfigurationName)))         { parameters.Add("IpConfigurationName", IpConfigurationName!); }
        if (bound.ContainsKey(nameof(DnsServer)))                   { parameters.Add("DnsServer", DnsServer!); }
        if (bound.ContainsKey(nameof(InternalDnsNameLabel)))        { parameters.Add("InternalDnsNameLabel", InternalDnsNameLabel!); }
        if (bound.ContainsKey(nameof(EnableAcceleratedNetworking))) { parameters.Add("EnableAcceleratedNetworking", EnableAcceleratedNetworking.IsPresent); }
        if (bound.ContainsKey(nameof(EnableIPForwarding)))          { parameters.Add("EnableIPForwarding", EnableIPForwarding.IsPresent); }
        if (bound.ContainsKey(nameof(EdgeZone)))                    { parameters.Add("EdgeZone", EdgeZone!); }
        if (bound.ContainsKey(nameof(Tag)))                         { parameters.Add("Tag", ToKeyValuePairs(Tag!)); }
        if (bound.ContainsKey(nameof(Profile)))                     { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Network", "New-AzNetworkInterface", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    // The processor takes KEY=VALUE tokens on both surfaces; PowerShell users expect hashtables.
    private static string[] ToKeyValuePairs(Hashtable table) =>
        table.Cast<DictionaryEntry>()
            .Select(entry => $"{entry.Key}={entry.Value}")
            .ToArray();

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
