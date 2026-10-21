// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsCommon.New, "AzVM", DefaultParameterSetName = NewNetworkInterfaceParameterSet, SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine")]
public class NewVMCmdlet : PSCmdlet
{
    private const string NewNetworkInterfaceParameterSet = "NewNetworkInterface";
    private const string ExistingNetworkInterfaceParameterSet = "ExistingNetworkInterface";

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

    [Parameter(Mandatory = false)]
    [Alias("ImageName")]
    public string? Image { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("TrustedLaunch", "ConfidentialVM", "Standard", IgnoreCase = true)]
    public string? SecurityType { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("VMGuestStateOnly", "DiskWithVMGuestState", "NonPersistedTPM", IgnoreCase = true)]
    public string? OSDiskSecurityEncryptionType { get; set; }

    [Parameter(Mandatory = false)]
    public string? OSDiskSecureVMDiskEncryptionSet { get; set; }

    [Parameter(Mandatory = true)]
    public string AdminUsername { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdminPassword { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string NetworkInterfaceName { get; set; } = "";

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string VirtualNetworkName { get; set; } = "";

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string? SecurityGroupName { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string? SubnetName { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ExistingNetworkInterfaceParameterSet)]
    public string[]? NetworkInterfaceId { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Delete", "Detach", IgnoreCase = true)]
    public string? NetworkInterfaceDeleteOption { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string[]? SubnetNames { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string[][]? SubnetAddressPrefixes { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string[]? AddressPrefixes { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public SwitchParameter AcceleratedNetworking { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = NewNetworkInterfaceParameterSet)]
    public string? PublicIpAddressName { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? Tags { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))                 { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Image)))                          { parameters.Add("Image", Image!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecurityType)))                   { parameters.Add("SecurityType", SecurityType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(OSDiskSecurityEncryptionType)))   { parameters.Add("OsDiskSecurityEncryptionType", OSDiskSecurityEncryptionType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(OSDiskSecureVMDiskEncryptionSet))){ parameters.Add("OsDiskSecureVMDiskEncryptionSet", OSDiskSecureVMDiskEncryptionSet!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkInterfaceName)))           { parameters.Add("NetworkInterfaceName", NetworkInterfaceName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VirtualNetworkName)))             { parameters.Add("VirtualNetworkName", VirtualNetworkName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetName)))                     { parameters.Add("SubnetName", SubnetName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkInterfaceId)))             { parameters.Add("NetworkInterfaceIds", NetworkInterfaceId!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkInterfaceDeleteOption)))   { parameters.Add("NetworkInterfaceDeleteOption", NetworkInterfaceDeleteOption!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetNames)))                    { parameters.Add("SubnetNames", SubnetNames!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetAddressPrefixes)))          { parameters.Add("SubnetAddressPrefixes", SubnetAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AddressPrefixes)))                { parameters.Add("AddressPrefixes", AddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AcceleratedNetworking)))          { parameters.Add("AcceleratedNetworking", AcceleratedNetworking.IsPresent); }
        // Omitted or $null keeps the default {vm}NSG; "" is forwarded because it means none.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecurityGroupName)) && SecurityGroupName is not null) { parameters.Add("SecurityGroupName", SecurityGroupName); }
        // Omitted or $null keeps the default public IP; "" is forwarded because it means none.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIpAddressName)) && PublicIpAddressName is not null) { parameters.Add("PublicIpAddressName", PublicIpAddressName); }
        // Omitted or $null sends no tags.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tags)) && Tags is not null) { parameters.Add("Tags", VmCmdletArgumentConverter.ToTagsInput(Tags)); }

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
