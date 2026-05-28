using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors
{
    public class NewVMProcessor : ComputeProcessorBase<VirtualMachine>
    {
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
        public string NetworkInterfaceId { get; set; } = string.Empty;
        public string SecurityType { get; set; } = "trustedlaunch";
        public bool SecureBootEnabled { get; set; } = true;
        public bool VTpmEnabled { get; set; } = true;
        public bool BootDiagnosticsEnabled { get; set; } = false;

        public override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";
            VirtualMachine body = new VirtualMachine
            {
                Location = Location,
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
                        WindowsConfiguration = new WindowsConfiguration()
                    },
                    NetworkProfile = new NetworkProfile
                    {
                        NetworkInterfaces =
                        [
                            new NetworkInterfaceReference { Id = NetworkInterfaceId }
                        ]
                    },
                    SecurityProfile = new SecurityProfile
                    {
                        SecurityType = SecurityType,
                        UefiSettings = new UefiSettings
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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
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



