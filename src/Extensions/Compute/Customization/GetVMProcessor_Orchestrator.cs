// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared read orchestrator behind <c>az vm show</c>, <c>Get-AzVM_Get</c> and
    /// <c>Get-AzVM_Id</c>. It owns the client-facing switches and reduces them to a single ARM
    /// <c>$expand</c> plus, when addressing by resource id, one GET per id.
    /// <c>ExtensionIndex</c> decides which switches each client exposes.
    /// </summary>
    public class GetVMProcessor_Orchestrator : GetVMProcessor
    {
        public string[]? Ids { get; set; }
        public bool IncludeUserData { get; set; }
        public bool ShowDetails { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = ResolveExpand();

            if (Ids is null || Ids.Length == 0)
            {
                EnsureAddressable();
                await base.ProcessAsync();
                return;
            }

            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                // One GET per id, aggregated into a single array write — the shape
                // ShowIdentityProcessor_Orchestrator established for --ids.
                var results = new List<VirtualMachine>();
                foreach (var id in Ids)
                {
                    var vmId = VirtualMachineIdResolver.ParseVirtualMachineResourceId(id);
                    using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                    {
                        p.SubscriptionId = vmId.SubscriptionId;
                        p.ResourceGroupName = vmId.ResourceGroupName;
                        p.Name = vmId.Name;
                        p.Expand = Expand;
                    }).SendAsync();

                    if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is { } vm)
                    {
                        if (ShowDetails)
                        {
                            await EnrichAsync(vm);
                        }
                        results.Add(vm);
                    }
                }

                await WriteOutput(results.ToArray());
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>
        /// The one place the switches turn into a request. Mirrors legacy `az vm show` exactly:
        /// --show-details needs the instance view for the power state, --include-user-data needs
        /// userData, and both together ask for one comma-joined expand.
        /// </summary>
        private string? ResolveExpand() => (ShowDetails, IncludeUserData) switch
        {
            (true, true) => "instanceView,userData",
            (true, false) => "instanceView",
            (false, true) => "userData",
            _ => null,
        };

        /// <summary>
        /// The base pipeline deserializes then writes. This is the seam that lets --show-details
        /// decorate the model in between; returning false leaves the write to the base.
        /// </summary>
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachine? body)
        {
            if (ShowDetails && body is not null)
            {
                await EnrichAsync(body);
            }
            return false;
        }

        /// <summary>
        /// ParameterMetadata cannot express "required unless --ids", so without this guard the
        /// binder accepts an empty name and ARM answers a confusing 404 for a malformed URL.
        /// </summary>
        private void EnsureAddressable()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName) || string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException(
                    "Specify the virtual machine by both name and resource group, or address it by resource id.");
            }
        }

        /// <summary>
        /// Resolves every NIC and public IP the VM references, flattens them into the comma-joined
        /// summary fields legacy `az vm show -d` produces, then drops the instance view.
        /// </summary>
        private async Task EnrichAsync(VirtualMachine vm)
        {
            var privateIps = new List<string>();
            var publicIps = new List<string>();
            var fqdns = new List<string>();
            var macAddresses = new List<string>();

            foreach (var nicReference in vm.Properties?.NetworkProfile?.NetworkInterfaces ?? [])
            {
                var nicId = VirtualMachineIdResolver.ParseResourceId(
                    nicReference.Id, "Microsoft.Network", "networkInterfaces", "Network interface", "networkInterfaceName");

                using HttpResponseMessage nicResponse = await CreateExtendedProcessor<GetNetworkInterfaceProcessor>(p =>
                {
                    p.SubscriptionId = nicId.SubscriptionId;
                    p.ResourceGroupName = nicId.ResourceGroupName;
                    p.Name = nicId.Name;
                }).SendAsync();

                if (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions) is not { } nic)
                {
                    continue;
                }

                Collect(macAddresses, nic.Properties?.MacAddress);

                foreach (var ipConfiguration in nic.Properties?.IPConfigurations ?? [])
                {
                    Collect(privateIps, ipConfiguration.Properties?.PrivateIPAddress);

                    if (ipConfiguration.Properties?.PublicIPAddress?.Id is not { } publicIpReference)
                    {
                        continue;
                    }

                    var publicIpId = VirtualMachineIdResolver.ParseResourceId(
                        publicIpReference, "Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");

                    using HttpResponseMessage publicIpResponse = await CreateExtendedProcessor<GetPublicIPAddressProcessor>(p =>
                    {
                        p.SubscriptionId = publicIpId.SubscriptionId;
                        p.ResourceGroupName = publicIpId.ResourceGroupName;
                        p.Name = publicIpId.Name;
                    }).SendAsync();

                    if (await publicIpResponse.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions) is not { } publicIp)
                    {
                        continue;
                    }

                    Collect(publicIps, publicIp.Properties?.IpAddress);
                    Collect(fqdns, publicIp.Properties?.DnsSettings?.Fqdn);
                }
            }

            vm.PowerState = string.Join(",", (vm.Properties?.InstanceView?.Statuses ?? [])
                .Where(status => status.Code?.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase) == true)
                .Select(status => status.DisplayStatus)
                .Where(displayStatus => !string.IsNullOrWhiteSpace(displayStatus)));

            vm.PublicIps = string.Join(",", publicIps);
            vm.Fqdns = string.Join(",", fqdns);
            vm.PrivateIps = string.Join(",", privateIps);
            vm.MacAddresses = string.Join(",", macAddresses);

            // Legacy drops the instance view once the power state is flattened out of it.
            if (vm.Properties is not null)
            {
                vm.Properties.InstanceView = null;
            }

            // Blank entries are skipped so a VM without a public IP yields "" rather than ",".
            static void Collect(List<string> target, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
        }
    }
}
