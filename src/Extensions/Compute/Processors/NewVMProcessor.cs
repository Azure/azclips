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
        public string? AdminPassword { get; set; }
        public string OsDiskCreateOption { get; set; } = "FromImage";
        public string ImagePublisher { get; set; } = "MicrosoftWindowsServer";
        public string ImageOffer { get; set; } = "WindowsServer";
        public string ImageSku { get; set; } = "2022-datacenter-azure-edition";
        public string ImageVersion { get; set; } = "latest";
        public string? PlanName { get; set; }
        public string? PlanProduct { get; set; }
        public string? PlanPublisher { get; set; }
        public string NetworkInterfaceId { get; set; } = string.Empty;
        public LinuxConfiguration? LinuxConfiguration { get; set; }
        public WindowsConfiguration? WindowsConfiguration { get; set; }
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
                Plan = PlanName is null && PlanProduct is null && PlanPublisher is null
                    ? null
                    : new ImagePlan
                    {
                        Name = PlanName,
                        Product = PlanProduct,
                        Publisher = PlanPublisher
                    },
                Properties = new VirtualMachineProperties
                {
                    HardwareProfile = new HardwareProfile(VmSize),
                    StorageProfile = new StorageProfile
                    {
                        ImageReference = new ImageReference
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
                        WindowsConfiguration = WindowsConfiguration,
                        LinuxConfiguration = LinuxConfiguration
                    },
                    NetworkProfile = new NetworkProfile
                    {
                        NetworkInterfaces =
                        [
                            new NetworkInterfaceReference { Id = NetworkInterfaceId }
                        ]
                    },
                    SecurityProfile = string.IsNullOrEmpty(SecurityType) && SecureBootEnabled is null && VTpmEnabled is null
                        ? null
                        : new SecurityProfile
                        {
                            SecurityType = SecurityType,
                            UefiSettings = SecureBootEnabled is null && VTpmEnabled is null
                                ? null
                                : new UefiSettings
                                {
                                    SecureBootEnabled = SecureBootEnabled,
                                    VTpmEnabled = VTpmEnabled
                                }
                        },
                    DiagnosticsProfile = new DiagnosticsProfile
                    {
                        BootDiagnostics = new BootDiagnostics { Enabled = BootDiagnosticsEnabled }
                    }
                }
            };
            return body;
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
