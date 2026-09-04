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

    [Parameter(Mandatory = false)]
    public string NetworkInterfaceName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string VirtualNetworkName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string NetworkSecurityGroupName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string PublicIPAddressId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string PublicIPAddressName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string PublicIPAddressDomainNameLabel { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string PublicIPAddressSku { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string PublicIPAddressAllocationMethod { get; set; } = "";

    [Parameter(Mandatory = false)]
    public bool NoPublicIPAddress { get; set; }

    [Parameter(Mandatory = false)]
    public bool EnableAcceleratedNetworking { get; set; }

    [Parameter(Mandatory = false)]
    public bool DisableAcceleratedNetworking { get; set; }

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
    public string OsDiskCreateOption { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string ImageId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string ImagePublisher { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string ImageOffer { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string ImageSku { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string ImageVersion { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string SecurityType { get; set; } = "";

    [Parameter(Mandatory = false)]
    public bool SecureBootEnabled { get; set; } = true;

    [Parameter(Mandatory = false)]
    public bool VTpmEnabled { get; set; } = true;

    [Parameter(Mandatory = false)]
    public bool BootDiagnosticsEnabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool DisableBootDiagnostics { get; set; }

    [Parameter(Mandatory = false)]
    public string BootDiagnosticsStorageUri { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string BootDiagnosticsStorageAccountUri { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string IdentityType { get; set; } = "";

    [Parameter(Mandatory = false)]
    public bool SystemAssignedIdentity { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? UserAssignedIdentityIds { get; set; }

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
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))                { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkInterfaceName)))          { parameters.Add("NetworkInterfaceName", NetworkInterfaceName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VirtualNetworkName)))            { parameters.Add("VirtualNetworkName", VirtualNetworkName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkSecurityGroupName)))      { parameters.Add("NetworkSecurityGroupName", NetworkSecurityGroupName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIPAddressId)))             { parameters.Add("PublicIPAddressId", PublicIPAddressId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIPAddressName)))           { parameters.Add("PublicIPAddressName", PublicIPAddressName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIPAddressDomainNameLabel))) { parameters.Add("PublicIPAddressDomainNameLabel", PublicIPAddressDomainNameLabel); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIPAddressSku)))            { parameters.Add("PublicIPAddressSku", PublicIPAddressSku); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIPAddressAllocationMethod))) { parameters.Add("PublicIPAddressAllocationMethod", PublicIPAddressAllocationMethod); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NoPublicIPAddress)))             { parameters.Add("NoPublicIPAddress", NoPublicIPAddress); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableAcceleratedNetworking)))   { parameters.Add("EnableAcceleratedNetworking", EnableAcceleratedNetworking); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DisableAcceleratedNetworking)))  { parameters.Add("DisableAcceleratedNetworking", DisableAcceleratedNetworking); }
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(OsDiskCreateOption)))             { parameters.Add("OsDiskCreateOption", OsDiskCreateOption); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageId)))                        { parameters.Add("ImageId", ImageId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImagePublisher)))                 { parameters.Add("ImagePublisher", ImagePublisher); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageOffer)))                     { parameters.Add("ImageOffer", ImageOffer); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageSku)))                       { parameters.Add("ImageSku", ImageSku); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageVersion)))                   { parameters.Add("ImageVersion", ImageVersion); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecurityType)))                   { parameters.Add("SecurityType", SecurityType); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecureBootEnabled)))              { parameters.Add("SecureBootEnabled", SecureBootEnabled); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VTpmEnabled)))                    { parameters.Add("VTpmEnabled", VTpmEnabled); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BootDiagnosticsEnabled)))         { parameters.Add("BootDiagnosticsEnabled", BootDiagnosticsEnabled); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DisableBootDiagnostics)))         { parameters.Add("DisableBootDiagnostics", DisableBootDiagnostics); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BootDiagnosticsStorageUri)))      { parameters.Add("BootDiagnosticsStorageUri", BootDiagnosticsStorageUri); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BootDiagnosticsStorageAccountUri))) { parameters.Add("BootDiagnosticsStorageAccountUri", BootDiagnosticsStorageAccountUri); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IdentityType)))                   { parameters.Add("IdentityType", IdentityType); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SystemAssignedIdentity)))         { parameters.Add("SystemAssignedIdentity", SystemAssignedIdentity); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UserAssignedIdentityIds)))        { parameters.Add("UserAssignedIdentityIds", UserAssignedIdentityIds!); }

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
