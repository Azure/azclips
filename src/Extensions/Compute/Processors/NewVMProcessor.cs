// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    public class NewVMProcessor : ComputeProcessorBase<VirtualMachine, VirtualMachine>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string VmSize { get; set; } = string.Empty;
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
        public string OsDiskCreateOption { get; set; } = "FromImage";
        public string ImagePublisher { get; set; } = "MicrosoftWindowsServer";
        public string ImageOffer { get; set; } = "WindowsServer";
        public string ImageSku { get; set; } = "2022-datacenter-azure-edition";
        public string ImageVersion { get; set; } = "latest";
        public ImageReference? ImageReference { get; set; }
        public ImagePlan? Plan { get; set; }
        public string NetworkInterfaceId { get; set; } = string.Empty;
        public string OperatingSystemType { get; set; } = OperatingSystemTypes.Windows;
        public string? SecurityType { get; set; } = "trustedlaunch";
        public bool? SecureBootEnabled { get; set; } = true;
        public bool? VTpmEnabled { get; set; } = true;
        public bool BootDiagnosticsEnabled { get; set; } = false;

        protected override HttpMethod Method => HttpMethod.Put;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";

        protected override VirtualMachine? CreateRequestBody()
        {
            VirtualMachine body = new VirtualMachine
            {
                Location = Location,
                Plan = Plan,
                Properties = new VirtualMachineProperties
                {
                    HardwareProfile = new HardwareProfile(VmSize),
                    StorageProfile = new StorageProfile
                    {
                        ImageReference = ImageReference ?? new ImageReference
                        {
                            Publisher = ImagePublisher,
                            Offer = ImageOffer,
                            Sku = ImageSku,
                            Version = ImageVersion
                        }
                    },
                    OSProfile = new OSProfile
                    {
                        ComputerName = Name,
                        AdminUsername = AdminUsername,
                        AdminPassword = AdminPassword,
                        WindowsConfiguration = IsWindows ? new WindowsConfiguration() : null
                    },
                    NetworkProfile = new NetworkProfile
                    {
                        NetworkInterfaces =
                        [
                            new NetworkInterfaceReference { Id = NetworkInterfaceId }
                        ]
                    },
                    SecurityProfile = CreateSecurityProfile(),
                    DiagnosticsProfile = new DiagnosticsProfile
                    {
                        BootDiagnostics = new BootDiagnostics { Enabled = BootDiagnosticsEnabled }
                    }
                }
            };
            return body;
        }

        private bool IsWindows => OperatingSystemType.Equals(OperatingSystemTypes.Windows, StringComparison.OrdinalIgnoreCase);

        private SecurityProfile? CreateSecurityProfile()
        {
            if (string.IsNullOrEmpty(SecurityType) && SecureBootEnabled is null && VTpmEnabled is null)
            {
                return null;
            }

            return new SecurityProfile
            {
                SecurityType = SecurityType,
                UefiSettings = SecureBootEnabled is null && VTpmEnabled is null
                    ? null
                    : new UefiSettings
                    {
                        SecureBootEnabled = SecureBootEnabled,
                        VTpmEnabled = VTpmEnabled
                    }
            };
        }

        /*
        Yabo:
            swagger says:         
            "x-ms-long-running-operation-options": {
                "final-state-via": "location"
            }
            but got nothing, fallback to no finaluri
        */
        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
    }
}
