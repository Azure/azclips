using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors
{
    public class NewVMProcessor : ComputeProcessorBase
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

        public override async Task<HttpRequestMessage> CreateRequestMessage(string? subscriptionId = null, IEventListener? eventListener = null, JsonSerializerOptions? jsonOptions = null)
        {
            eventListener ??= EventListener;
            await eventListener.Signal(Events.ProcessorBeforeHttpRequestCreate);
            subscriptionId ??= SubscriptionId;
            jsonOptions ??= _jsonOptions;
            string uri = $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";
            VirtualMachine body = new VirtualMachine
            {
                Name = Name,
                Location = Location,
                ResourceGroupName = ResourceGroupName,
                HardwareProfile = new HardwareProfile(VmSize),
                OSProfile = new OSProfile
                {
                    AdminUsername = AdminUsername,
                    AdminPassword = AdminPassword
                },
                StorageProfile = new StorageProfile
                {
                    OSDisk = new OSDisk { CreateOption = OsDiskCreateOption },
                    ImageReference = new ImageReference
                    {
                        Publisher = ImagePublisher,
                        Offer = ImageOffer,
                        Sku = ImageSku,
                        Version = ImageVersion
                    }
                }
            };
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Content = JsonContent.Create(body, options: jsonOptions)
            };
            await eventListener.Signal(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            SubscriptionId = SubscriptionId ?? AuthContext.SubscriptionId ?? throw new InvalidOperationException($"SubscriptionId must be provided either via property or auth context.");
            using HttpResponseMessage response = await SendAsyncLRO();
            await EventListener.Signal(Events.TracingHttpResponseMessage, response);
            VirtualMachine body = (await response.Content.ReadFromJsonAsync<VirtualMachine>(_jsonOptions))!;
            // TODO(Yabo): handle status codes
            await WriteOutput(body);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}