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

        public string? Image { get; set; }

        [ParameterGroup("Security Arguments")]
        public string? SecurityType { get; set; }

        [ParameterGroup("Security Arguments")]
        public string? OsDiskSecurityEncryptionType { get; set; }

        [ParameterGroup("Security Arguments")]
        public string? OsDiskSecureVMDiskEncryptionSet { get; set; }

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
        public string NetworkSecurityGroupName { get; set; } = string.Empty;

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

        [ParameterGroup("Network Arguments")]
        public bool AcceleratedNetworking { get; set; }

        [ParameterGroup("Network Arguments")]
        public string? PublicIpAddress { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            if (string.IsNullOrWhiteSpace(AdminPassword) && SshKeyValues.Count == 0)
            {
                throw new ArgumentException("Specify an administrator password or SSH public key values.");
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
                SubscriptionId,
                ResourceGroupName);

            if (SshKeyValues.Count > 0 && !operatingSystemType.Equals(OperatingSystemTypes.Linux, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("SSH public keys require a Linux marketplace image. Specify --image with a Linux URN.");
            }

            ResolvedLinuxProfile? linuxProfile = null;
            if (operatingSystemType.Equals(OperatingSystemTypes.Linux, StringComparison.OrdinalIgnoreCase))
            {
                linuxProfile = OSProfileResolver.Resolve(SshKeyValues, AdminUsername, AdminPassword);
            }

             NewNetworkInterfaceProcessor_Orchestrator nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Orchestrator>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = NetworkInterfaceName;
                p.Location = Location;
                p.ResourceGroupName = ResourceGroupName;
                p.VirtualNetworkName = VirtualNetworkName;
                p.NetworkSecurityGroupName = NetworkSecurityGroupName;
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
                p.AcceleratedNetworking = AcceleratedNetworking;
                p.PublicIpAddress = PublicIpAddress;
                p.DefaultPublicIpAddressName = $"{Name}PublicIP";
            });
            using HttpResponseMessage nicResponse = await nicProcessor.SendAsync();
            NetworkInterface nic = (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions))!;

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
                p.NetworkInterfaceId = nic.Id;
             });
            return await vmProcessor.SendAsync();
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
        /// For the time being shows az vm show -d output, with enriched network details
        /// TODO: Jayson - client side rendering of views for different clients.
        /// </summary>
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachine? body)
        {
            if (body is null)
            {
                return false;
            }

            VirtualMachine output = body;
            try
            {
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
                output = detailed;
            }
            catch (Exception exception)
            {
                await Publisher.Publish(Events.Warning, new EventData
                {
                    Message = $"Created virtual machine '{Name}' but could not retrieve its details: {exception.Message}"
                });
            }

            await WriteOutput(output);
            return true;
        }
    }
}
