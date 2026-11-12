// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
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

        public string? Image { get; set; }

        public string[]? TagsInput { get; set; }

        public string? Zone { get; set; }

        public string? StorageSku { get; set; }

        public string? OsDiskDeleteOption { get; set; }

        [ParameterGroup("Security Arguments")]
        public string? SecurityType { get; set; }

        [ParameterGroup("Security Arguments")]
        public string? OsDiskSecurityEncryptionType { get; set; }

        [ParameterGroup("Security Arguments")]
        public string? OsDiskSecureVMDiskEncryptionSet { get; set; }

        [ParameterGroup("Security Arguments")]
        public bool? EnableSecureBoot { get; set; }

        [ParameterGroup("Security Arguments")]
        public bool? EnableVtpm { get; set; }

        [ParameterGroup("Authentication Arguments")]
        public string AdminUsername { get; set; } = string.Empty;

        [ParameterGroup("Authentication Arguments")]
        public string AdminPassword { get; set; } = string.Empty;

        [ParameterGroup("Authentication Arguments")]
        public List<string> SshKeyValues { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public string NetworkInterfaceName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string VirtualNetworkName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string? SubnetName { get; set; }

        [ParameterGroup("Network Arguments")]
        public List<string> NetworkInterfaceIds { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> NicDeleteOptions { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public string? NetworkInterfaceDeleteOption { get; set; }

        [ParameterGroup("Network Arguments")]
        public string? NetworkSecurityGroup { get; set; }

        [ParameterGroup("Network Arguments")]
        public List<string> SubnetNames { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<List<string>> SubnetAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> AddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public bool? AcceleratedNetworking { get; set; }

        [ParameterGroup("Network Arguments")]
        public string? PublicIpAddress { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            Dictionary<string, string>? tags = VmTagsParser.ParseTags(TagsInput);
            var osDiskStorageAccountType = OsDiskStorageSkuResolver.Resolve(StorageSku);
            ValidateNetworkArguments();

            if (string.IsNullOrWhiteSpace(AdminPassword) && SshKeyValues.Count == 0)
            {
                throw AzclipsErrorException.UsageError("Specify an administrator password or SSH public key values.");
            }

            var imageReference = ImageReferenceResolver.ResolveMarketplaceUrn(Image);
            var operatingSystemType = OperatingSystemTypes.Windows;
            ImagePlan? plan = null;
            var useTrustedLaunchDefaults = imageReference is null;
            if (imageReference is not null)
            {
                (operatingSystemType, plan, useTrustedLaunchDefaults) = await ResolveImageMetadataAsync(imageReference);
            }

            var securityProfile = VmSecurityProfileResolver.Resolve(
                SecurityType,
                useTrustedLaunchDefaults,
                OsDiskSecurityEncryptionType,
                OsDiskSecureVMDiskEncryptionSet,
                EnableSecureBoot,
                EnableVtpm,
                SubscriptionId,
                ResourceGroupName);

            if (SshKeyValues.Count > 0 && !operatingSystemType.Equals(OperatingSystemTypes.Linux, StringComparison.OrdinalIgnoreCase))
            {
                throw AzclipsErrorException.UsageError("SSH public keys require a Linux marketplace image. Specify a Linux marketplace image URN.");
            }

            ResolvedLinuxProfile? linuxProfile = null;
            if (operatingSystemType.Equals(OperatingSystemTypes.Linux, StringComparison.OrdinalIgnoreCase))
            {
                linuxProfile = OSProfileResolver.Resolve(SshKeyValues, AdminUsername, AdminPassword);
            }

            List<string> normalizedNetworkInterfaceIds;
            List<string?> networkInterfaceDeleteOptions;
            string? createdNetworkInterfaceDeleteOption = null;
            if (NetworkInterfaceIds.Count > 0)
            {
                normalizedNetworkInterfaceIds = NetworkInterfaceIds.Select(NormalizeNetworkInterfaceId).ToList();
                networkInterfaceDeleteOptions = NicDeleteOptionResolver.ResolveForExistingNics(
                    NetworkInterfaceIds, NicDeleteOptions, NetworkInterfaceDeleteOption);
            }
            else
            {
                createdNetworkInterfaceDeleteOption =
                    NicDeleteOptionResolver.ResolveForNewNic(NicDeleteOptions, NetworkInterfaceDeleteOption);
                NewNetworkInterfaceProcessor_Orchestrator nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Orchestrator>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.Name = NetworkInterfaceName;
                    p.Location = Location;
                    p.ResourceGroupName = ResourceGroupName;
                    p.VirtualNetworkName = VirtualNetworkName;
                    p.SubnetName = SubnetName;
                    p.NetworkSecurityGroup = NetworkSecurityGroup;
                    p.DefaultNetworkSecurityGroupName = $"{Name}NSG";
                    p.SubnetNames = SubnetNames;
                    p.SubnetAddressPrefixes = SubnetAddressPrefixes;
                    p.AddressPrefixes = AddressPrefixes;
                    p.AcceleratedNetworking = AcceleratedNetworking ?? false;
                    p.PublicIpAddress = PublicIpAddress;
                    p.DefaultPublicIpAddressName = $"{Name}PublicIP";
                    p.OsType = operatingSystemType;
                    p.Tags = tags;
                });
                using HttpResponseMessage nicResponse = await nicProcessor.SendAsync();
                NetworkInterface nic = (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions))!;
                normalizedNetworkInterfaceIds = [nic.Id];
                networkInterfaceDeleteOptions = [];
            }

            NewVMProcessor vmProcessor = CreateExtendedProcessor<NewVMProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.Location = Location;
                p.ResourceGroupName = ResourceGroupName;
                p.VmSize = VmSize;
                if (imageReference is not null)
                {
                    p.ImagePublisher = imageReference.Publisher!;
                    p.ImageOffer = imageReference.Offer!;
                    p.ImageSku = imageReference.Sku!;
                    p.ImageVersion = imageReference.Version!;
                }
                p.PlanName = plan?.Name;
                p.PlanProduct = plan?.Product;
                p.PlanPublisher = plan?.Publisher;
                p.Zones = string.IsNullOrWhiteSpace(Zone) ? [] : [Zone];
                p.OsDiskStorageAccountType = osDiskStorageAccountType;
                p.OsDiskDeleteOption = OsDiskDeleteOption;
                p.AdminUsername = AdminUsername;
                p.AdminPassword = string.IsNullOrEmpty(AdminPassword) ? null : AdminPassword;
                p.IncludeWindowsConfiguration = operatingSystemType.Equals(OperatingSystemTypes.Windows, StringComparison.OrdinalIgnoreCase);
                p.SecurityType = securityProfile.SecurityType;
                p.SecureBootEnabled = securityProfile.SecureBootEnabled;
                p.VTpmEnabled = securityProfile.VTpmEnabled;
                p.OsDiskSecurityEncryptionType = securityProfile.OsDiskSecurityEncryptionType;
                p.OsDiskSecureVMDiskEncryptionSetId = securityProfile.OsDiskSecureVMDiskEncryptionSetId;
                if (linuxProfile is not null)
                {
                    p.LinuxDisablePasswordAuthentication = linuxProfile.DisablePasswordAuthentication;
                    p.LinuxSshPublicKeyPaths = linuxProfile.SshPublicKeyPaths;
                    p.LinuxSshPublicKeyData = linuxProfile.SshPublicKeyData;
                }
                if (NetworkInterfaceIds.Count > 0)
                {
                    p.NetworkInterfaceIds = normalizedNetworkInterfaceIds;
                    p.NetworkInterfaceDeleteOptions = networkInterfaceDeleteOptions;
                }
                else
                {
                    p.NetworkInterfaceId = normalizedNetworkInterfaceIds[0];
                    p.NetworkInterfaceDeleteOption = createdNetworkInterfaceDeleteOption;
                }
                p.Tags = tags;
             });
            return await vmProcessor.SendAsync();
        }

        private void ValidateNetworkArguments()
        {
            if (NetworkInterfaceIds.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(NetworkInterfaceName))
                {
                    throw AzclipsErrorException.UsageError(
                        "Specify a name for the new network interface when existing network interfaces are not supplied.");
                }
                return;
            }

            var conflicts = new List<string>();
            if (!string.IsNullOrWhiteSpace(NetworkInterfaceName)) conflicts.Add("a new network interface name");
            if (!string.IsNullOrWhiteSpace(VirtualNetworkName)) conflicts.Add("a virtual network name");
            if (SubnetName is not null) conflicts.Add("a subnet");
            if (SubnetNames.Count > 0) conflicts.Add("subnet names");
            if (SubnetAddressPrefixes.Count > 0) conflicts.Add("subnet address prefixes");
            if (AddressPrefixes.Count > 0) conflicts.Add("virtual network address prefixes");
            if (NetworkSecurityGroup is not null) conflicts.Add("a network security group");
            if (PublicIpAddress is not null) conflicts.Add("a public IP address");
            if (AcceleratedNetworking is not null) conflicts.Add("an accelerated networking setting");

            if (conflicts.Count > 0)
            {
                throw AzclipsErrorException.UsageError(
                    $"When specifying existing NICs, do not specify {string.Join(", ", conflicts)}.");
            }
        }

        private string NormalizeNetworkInterfaceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw AzclipsErrorException.UsageError("Network interface name or resource ID cannot be empty.");
            }

            if (!value.Contains('/'))
            {
                return $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{value}";
            }

            var parsed = VirtualMachineIdResolver.ParseResourceId(
                value, "Microsoft.Network", "networkInterfaces", "Network interface", "networkInterfaceName");
            return $"/subscriptions/{parsed.SubscriptionId}/resourceGroups/{parsed.ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{parsed.Name}";
        }

        private async Task<(string OperatingSystemType, ImagePlan? Plan, bool UseTrustedLaunchDefaults)> ResolveImageMetadataAsync(ImageReference imageReference)
        {
            var version = imageReference.Version!;
            if (version.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                var latestImage = await GetLatestImageVersionAsync(imageReference);
                version = latestImage.Name
                    ?? throw new InvalidOperationException($"No image versions found for '{imageReference.Publisher}:{imageReference.Offer}:{imageReference.Sku}:latest'.");
                imageReference.Version = version;
            }

            GetVMImageProcessor getImageProcessor = CreateExtendedProcessor<GetVMImageProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Location = Location;
                p.ImagePublisher = imageReference.Publisher!;
                p.Offer = imageReference.Offer!;
                p.Sku = imageReference.Sku!;
                p.Version = version;
            });
            using HttpResponseMessage imageResponse = await getImageProcessor.SendAsync();
            var image = (await imageResponse.Content.ReadFromJsonAsync<VirtualMachineImage>(JsonOptions))!;

            var operatingSystemType = image.Properties?.OsDiskImage?.OperatingSystem
                ?? throw new InvalidOperationException("Image metadata did not include properties.osDiskImage.operatingSystem.");
            return (operatingSystemType, image.Properties?.Plan, SupportsTrustedLaunch(image.Properties));
        }

        private static bool SupportsTrustedLaunch(VirtualMachineImageProperties? properties)
        {
            if (properties is null
                || !string.Equals(properties.HyperVGeneration, "V2", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return properties.Features.Any(feature =>
                string.Equals(feature.Name, "SecurityType", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(feature.Value, "TrustedLaunch", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(feature.Value, "TrustedLaunchSupported", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(feature.Value, "TrustedLaunchAndConfidentialVmSupported", StringComparison.OrdinalIgnoreCase)));
        }

        private async Task<VirtualMachineImage> GetLatestImageVersionAsync(ImageReference imageReference)
        {
            ListVMImageVersionsProcessor listVersionsProcessor = CreateExtendedProcessor<ListVMImageVersionsProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Location = Location;
                p.ImagePublisher = imageReference.Publisher!;
                p.Offer = imageReference.Offer!;
                p.Sku = imageReference.Sku!;
            });
            using HttpResponseMessage versionsResponse = await listVersionsProcessor.SendAsync();
            var versions = (await versionsResponse.Content.ReadFromJsonAsync<List<VirtualMachineImage>>(JsonOptions))!;
            return versions.OrderByDescending(image => image.Name, Comparer<string?>.Create(CompareImageVersions)).FirstOrDefault()
                ?? throw new InvalidOperationException($"No image versions found for '{imageReference.Publisher}:{imageReference.Offer}:{imageReference.Sku}:latest'.");
        }

        private static int CompareImageVersions(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left))
            {
                return string.IsNullOrWhiteSpace(right) ? 0 : -1;
            }

            if (string.IsNullOrWhiteSpace(right))
            {
                return 1;
            }

            var leftParsed = Version.TryParse(left, out var leftVersion);
            var rightParsed = Version.TryParse(right, out var rightVersion);

            return (leftParsed, rightParsed) switch
            {
                (true, true) => leftVersion!.CompareTo(rightVersion),
                (true, false) => 1,
                (false, true) => -1,
                _ => StringComparer.OrdinalIgnoreCase.Compare(left, right)
            };
        }

        /// <summary>
        /// Writes the created VM with the az vm show -d detail fields.
        /// As in legacy CLI, a failed detail read fails the command, although the VM itself was already created.
        /// TODO: Jayson - client side rendering of views for different clients.
        /// </summary>
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachine? body)
        {
            if (body is null)
            {
                return false;
            }

            using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
                p.Name = Name;
                p.Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: false);
            }).SendAsync();

            VirtualMachine detailed = (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions))!;
            await VirtualMachineDetailEnricher.ApplyNetworkAsync(this, detailed);
            VirtualMachineDetailEnricher.ApplyInstanceView(detailed, includeStatusFields: false);
            await WriteOutput(detailed);
            return true;
        }
    }
}
