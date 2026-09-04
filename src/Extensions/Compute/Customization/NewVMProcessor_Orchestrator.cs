// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    public class NewVMProcessor_Orchestrator : ComputeProcessorBase<VirtualMachine, VirtualMachine>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        [PopulatorCommand("az group list")]
        public string ResourceGroupName { get; set; } = string.Empty;

        public string VmSize { get; set; } = string.Empty;

        [ParameterGroup("Authentication Arguments")]
        public string AdminUsername { get; set; } = string.Empty;

        [ParameterGroup("Authentication Arguments")]
        public string AdminPassword { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string NetworkInterfaceName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string VirtualNetworkName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string NetworkSecurityGroupName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string PublicIPAddressId { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string PublicIPAddressName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string PublicIPAddressDomainNameLabel { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string PublicIPAddressSku { get; set; } = "Standard";

        [ParameterGroup("Network Arguments")]
        public string PublicIPAddressAllocationMethod { get; set; } = "Static";

        [ParameterGroup("Network Arguments")]
        public bool NoPublicIPAddress { get; set; }

        [ParameterGroup("Network Arguments")]
        public bool EnableAcceleratedNetworking { get; set; }

        [ParameterGroup("Network Arguments")]
        public bool DisableAcceleratedNetworking { get; set; }

        public string OsDiskCreateOption { get; set; } = "FromImage";
        public string ImageId { get; set; } = string.Empty;
        public string ImagePublisher { get; set; } = "MicrosoftWindowsServer";
        public string ImageOffer { get; set; } = "WindowsServer";
        public string ImageSku { get; set; } = "2022-datacenter-azure-edition";
        public string ImageVersion { get; set; } = "latest";
        public string SecurityType { get; set; } = "TrustedLaunch";
        public bool SecureBootEnabled { get; set; } = true;
        public bool VTpmEnabled { get; set; } = true;
        public bool BootDiagnosticsEnabled { get; set; }
        public bool DisableBootDiagnostics { get; set; }
        public string BootDiagnosticsStorageUri { get; set; } = string.Empty;
        public string BootDiagnosticsStorageAccountUri { get; set; } = string.Empty;
        public string IdentityType { get; set; } = string.Empty;
        public bool SystemAssignedIdentity { get; set; }
        public List<string> UserAssignedIdentityIds { get; set; } = [];
        public bool EnableIntegrityMonitoring { get; set; }
        public bool DisableIntegrityMonitoringAutoUpgrade { get; set; }

        [ParameterGroup("Network Arguments")]
        public List<string> SubnetNames { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<List<string>> SubnetAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> AddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleNames { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDirections { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleProtocols { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<int> RulePriorities { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleSourceAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleSourcePortRanges { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDestinationPortRanges { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDestinationAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleAccesses { get; set; } = [];

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            string networkInterfaceName = string.IsNullOrWhiteSpace(NetworkInterfaceName) ? $"{Name}-nic" : NetworkInterfaceName;
            string virtualNetworkName = string.IsNullOrWhiteSpace(VirtualNetworkName) ? $"{Name}-vnet" : VirtualNetworkName;
            string networkSecurityGroupName = string.IsNullOrWhiteSpace(NetworkSecurityGroupName) ? $"{Name}-nsg" : NetworkSecurityGroupName;

             NewNetworkInterfaceProcessor_Orchestrator nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Orchestrator>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = networkInterfaceName;
                p.Location = Location;
                p.ResourceGroupName = ResourceGroupName;
                p.VirtualNetworkName = virtualNetworkName;
                p.NetworkSecurityGroupName = networkSecurityGroupName;
                p.PublicIPAddressId = PublicIPAddressId;
                p.PublicIPAddressName = PublicIPAddressName;
                p.PublicIPAddressDomainNameLabel = PublicIPAddressDomainNameLabel;
                p.PublicIPAddressSku = PublicIPAddressSku;
                p.PublicIPAddressAllocationMethod = PublicIPAddressAllocationMethod;
                p.NoPublicIPAddress = NoPublicIPAddress;
                p.EnableAcceleratedNetworking = ResolveAcceleratedNetworking();
                p.SubnetNames = SubnetNames;
                p.SubnetAddressPrefixes = SubnetAddressPrefixes;
                p.AddressPrefixes = AddressPrefixes;
                p.RuleNames = RuleNames;
                p.RuleDirections = RuleDirections;
                p.RuleProtocols = RuleProtocols;
                p.RulePriorities = RulePriorities;
                p.RuleSourceAddressPrefixes = RuleSourceAddressPrefixes;
                p.RuleSourcePortRanges = RuleSourcePortRanges;
                p.RuleDestinationPortRanges = RuleDestinationPortRanges;
                p.RuleDestinationAddressPrefixes = RuleDestinationAddressPrefixes;
                p.RuleAccesses = RuleAccesses;
            });
            using HttpResponseMessage nicResponse = await nicProcessor.SendAsync();
            NetworkInterface nic = (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions))!;

            NewVirtualMachineProcessor_Customized vmProcessor = CreateExtendedProcessor<NewVirtualMachineProcessor_Customized>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.Location = Location;
                p.ResourceGroupName = ResourceGroupName;
                p.VmSize = VmSize;
                p.AdminUsername = AdminUsername;
                p.AdminPassword = AdminPassword;
                p.OsDiskCreateOption = OsDiskCreateOption;
                p.ImageId = ImageId;
                p.ImagePublisher = ImagePublisher;
                p.ImageOffer = ImageOffer;
                p.ImageSku = ImageSku;
                p.ImageVersion = ImageVersion;
                p.NetworkInterfaceId = nic.Id;
                p.SecurityType = EnableIntegrityMonitoring ? "TrustedLaunch" : SecurityType;
                p.SecureBootEnabled = EnableIntegrityMonitoring || SecureBootEnabled;
                p.VTpmEnabled = EnableIntegrityMonitoring || VTpmEnabled;
                p.BootDiagnosticsEnabled = BootDiagnosticsEnabled;
                p.DisableBootDiagnostics = DisableBootDiagnostics;
                p.BootDiagnosticsStorageUri = BootDiagnosticsStorageUri;
                p.BootDiagnosticsStorageAccountUri = BootDiagnosticsStorageAccountUri;
                p.IdentityType = IdentityType;
                p.SystemAssignedIdentity = SystemAssignedIdentity;
                p.UserAssignedIdentityIds = UserAssignedIdentityIds;
             });
            HttpResponseMessage vmResponse = await vmProcessor.SendAsync();

            if (EnableIntegrityMonitoring)
            {
                NewVMGuestAttestationExtensionProcessor_Orchestrator extensionProcessor = CreateExtendedProcessor<NewVMGuestAttestationExtensionProcessor_Orchestrator>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                    p.VMName = Name;
                    p.Location = Location;
                    p.ExtensionPublisher = ResolveGuestAttestationPublisher();
                    p.EnableAutomaticUpgrade = !DisableIntegrityMonitoringAutoUpgrade;
                });
                using HttpResponseMessage _ = await extensionProcessor.SendAsync();
            }

            return vmResponse;
        }

        private bool? ResolveAcceleratedNetworking()
        {
            if (EnableAcceleratedNetworking)
            {
                return true;
            }

            if (DisableAcceleratedNetworking)
            {
                return false;
            }

            return null;
        }

        private string ResolveGuestAttestationPublisher()
        {
            string image = string.Join(' ', ImageId, ImagePublisher, ImageOffer, ImageSku);
            return image.Contains("linux", StringComparison.OrdinalIgnoreCase)
                || image.Contains("ubuntu", StringComparison.OrdinalIgnoreCase)
                || image.Contains("canonical", StringComparison.OrdinalIgnoreCase)
                || image.Contains("debian", StringComparison.OrdinalIgnoreCase)
                || image.Contains("rhel", StringComparison.OrdinalIgnoreCase)
                || image.Contains("redhat", StringComparison.OrdinalIgnoreCase)
                || image.Contains("sles", StringComparison.OrdinalIgnoreCase)
                    ? "Microsoft.Azure.Security.LinuxAttestation"
                    : "Microsoft.Azure.Security.WindowsAttestation";
        }
    }
}