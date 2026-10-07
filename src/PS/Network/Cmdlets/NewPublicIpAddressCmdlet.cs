// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzPublicIpAddress", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIpAddress")]
public class NewPublicIpAddressCmdlet : PSCmdlet
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

    [Parameter(Mandatory = false)]
    [ValidateSet("Basic", "Standard", "StandardV2", IgnoreCase = true)]
    public string? Sku { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Regional", "Global", IgnoreCase = true)]
    public string? Tier { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Static", "Dynamic", IgnoreCase = true)]
    public string? AllocationMethod { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("IPv4", "IPv6", IgnoreCase = true)]
    public string? IpAddressVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? DomainNameLabel { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("TenantReuse", "SubscriptionReuse", "ResourceGroupReuse", "NoReuse", IgnoreCase = true)]
    public string? DomainNameLabelScope { get; set; }

    [Parameter(Mandatory = false)]
    public string? ReverseFqdn { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateRange(4, 30)]
    public int IdleTimeoutInMinutes { get; set; }

    [Parameter(Mandatory = false)]
    public string? IpAddress { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("IpTags")]
    public Hashtable? IpTag { get; set; }

    [Parameter(Mandatory = false)]
    public string? PublicIpPrefix { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("VirtualNetworkInherited", "Enabled", "Disabled", IgnoreCase = true)]
    public string? DdosProtectionMode { get; set; }

    [Parameter(Mandatory = false)]
    public string? DdosProtectionPlanId { get; set; }

    [Parameter(Mandatory = false)]
    public string? DdosCustomPolicyId { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Zones")]
    public string[]? Zone { get; set; }

    [Parameter(Mandatory = false)]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "PublicIpAddressError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"Public IP address '{Name}' in resource group '{ResourceGroupName}'", "Create"))
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
        if (bound.ContainsKey(nameof(SubscriptionId)))       { parameters.Add("SubscriptionId", SubscriptionId); }
        if (bound.ContainsKey(nameof(Sku)))                  { parameters.Add("Sku", Sku!); }
        if (bound.ContainsKey(nameof(Tier)))                 { parameters.Add("Tier", Tier!); }
        if (bound.ContainsKey(nameof(AllocationMethod)))     { parameters.Add("AllocationMethod", AllocationMethod!); }
        if (bound.ContainsKey(nameof(IpAddressVersion)))     { parameters.Add("IpAddressVersion", IpAddressVersion!); }
        if (bound.ContainsKey(nameof(DomainNameLabel)))      { parameters.Add("DomainNameLabel", DomainNameLabel!); }
        if (bound.ContainsKey(nameof(DomainNameLabelScope))) { parameters.Add("DomainNameLabelScope", DomainNameLabelScope!); }
        if (bound.ContainsKey(nameof(ReverseFqdn)))          { parameters.Add("ReverseFqdn", ReverseFqdn!); }
        if (bound.ContainsKey(nameof(IdleTimeoutInMinutes))) { parameters.Add("IdleTimeoutInMinutes", IdleTimeoutInMinutes); }
        if (bound.ContainsKey(nameof(IpAddress)))            { parameters.Add("IpAddress", IpAddress!); }
        if (bound.ContainsKey(nameof(IpTag)))                { parameters.Add("IpTag", ToKeyValuePairs(IpTag!)); }
        if (bound.ContainsKey(nameof(PublicIpPrefix)))       { parameters.Add("PublicIpPrefix", PublicIpPrefix!); }
        if (bound.ContainsKey(nameof(DdosProtectionMode)))   { parameters.Add("DdosProtectionMode", DdosProtectionMode!); }
        if (bound.ContainsKey(nameof(DdosProtectionPlanId))) { parameters.Add("DdosProtectionPlanId", DdosProtectionPlanId!); }
        if (bound.ContainsKey(nameof(DdosCustomPolicyId)))   { parameters.Add("DdosCustomPolicyId", DdosCustomPolicyId!); }
        if (bound.ContainsKey(nameof(Zone)))                 { parameters.Add("Zone", Zone!); }
        if (bound.ContainsKey(nameof(EdgeZone)))             { parameters.Add("EdgeZone", EdgeZone!); }
        if (bound.ContainsKey(nameof(Tag)))                  { parameters.Add("Tag", ToKeyValuePairs(Tag!)); }
        if (bound.ContainsKey(nameof(Profile)))              { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Network", "New-AzPublicIpAddress", parameters, false, moduleName, moduleVersion);
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
