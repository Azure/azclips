// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared detail enrichment behind <c>az vm show -d</c>, <c>az vm list -d</c> and
    /// <c>Get-AzVM -Status</c>. Two independent modes, so each caller pays only for what it exposes:
    /// <list type="bullet">
    /// <item><description><see cref="ApplyNetworkAsync"/> resolves NICs and public IPs — <c>--show-details</c> only.</description></item>
    /// <item><description><see cref="ApplyInstanceView"/> flattens the instance view — <c>--show-details</c> and <c>-Status</c>.</description></item>
    /// </list>
    /// Extracted from <c>GetVMProcessor_Orchestrator</c> so the CLI and PowerShell detail views
    /// cannot drift apart.
    /// </summary>
    internal static class VirtualMachineDetailEnricher
    {
        /// <summary>
        /// The one place detail switches turn into an ARM <c>$expand</c>. Mirrors legacy exactly:
        /// power-state views need the instance view, user data needs userData, and both together
        /// ask for one comma-joined expand rather than two round trips.
        /// </summary>
        internal static string? ResolveExpand(bool instanceView, bool userData) => (instanceView, userData) switch
        {
            (true, true) => "instanceView,userData",
            (true, false) => "instanceView",
            (false, true) => "userData",
            _ => null,
        };

        /// <summary>
        /// Resolves every NIC and public IP the VM references, flattening them into the comma-joined
        /// summary fields legacy <c>az vm show -d</c> produces. Blank entries are skipped so a VM
        /// without a public IP yields "" rather than ",".
        /// </summary>
        internal static async Task ApplyNetworkAsync(ComputeProcessorBase owner, VirtualMachine vm)
        {
            var privateIps = new List<string>();
            var publicIps = new List<string>();
            var fqdns = new List<string>();
            var macAddresses = new List<string>();

            foreach (var nicReference in vm.Properties?.NetworkProfile?.NetworkInterfaces ?? [])
            {
                var nicId = VirtualMachineIdResolver.ParseResourceId(
                    nicReference.Id, "Microsoft.Network", "networkInterfaces", "Network interface", "networkInterfaceName");

                using HttpResponseMessage nicResponse = await owner.CreateExtendedProcessor<GetNetworkInterfaceProcessor>(p =>
                {
                    p.SubscriptionId = nicId.SubscriptionId;
                    p.ResourceGroupName = nicId.ResourceGroupName;
                    p.Name = nicId.Name;
                }).SendAsync();

                if (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(owner.JsonOptions) is not { } nic)
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

                    using HttpResponseMessage publicIpResponse = await owner.CreateExtendedProcessor<GetPublicIPAddressProcessor>(p =>
                    {
                        p.SubscriptionId = publicIpId.SubscriptionId;
                        p.ResourceGroupName = publicIpId.ResourceGroupName;
                        p.Name = publicIpId.Name;
                    }).SendAsync();

                    if (await publicIpResponse.Content.ReadFromJsonAsync<PublicIPAddress>(owner.JsonOptions) is not { } publicIp)
                    {
                        continue;
                    }

                    Collect(publicIps, publicIp.Properties?.IpAddress);
                    Collect(fqdns, publicIp.Properties?.DnsSettings?.Fqdn);
                }
            }

            vm.PublicIps = string.Join(",", publicIps);
            vm.Fqdns = string.Join(",", fqdns);
            vm.PrivateIps = string.Join(",", privateIps);
            vm.MacAddresses = string.Join(",", macAddresses);

            static void Collect(List<string> target, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
        }

        /// <summary>
        /// Flattens the expanded instance view onto the model, then drops it — legacy does the same
        /// once the power state has been read out. <paramref name="includeStatusFields"/> adds the
        /// four fields only <c>Get-AzVM -Status</c> surfaces; <c>--show-details</c> passes false.
        /// </summary>
        internal static void ApplyInstanceView(VirtualMachine vm, bool includeStatusFields)
        {
            VirtualMachineInstanceView? instanceView = vm.Properties?.InstanceView;

            vm.PowerState = string.Join(",", (instanceView?.Statuses ?? [])
                .Where(status => status.Code?.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase) == true)
                .Select(status => status.DisplayStatus)
                .Where(displayStatus => !string.IsNullOrWhiteSpace(displayStatus)));

            if (includeStatusFields)
            {
                vm.OsName = instanceView?.OsName;
                vm.OsVersion = instanceView?.OsVersion;
                vm.HyperVGeneration = instanceView?.HyperVGeneration;
                vm.MaintenanceRedeployStatus = instanceView?.MaintenanceRedeployStatus;
            }

            if (vm.Properties is not null)
            {
                vm.Properties.InstanceView = null;
            }
        }
    }
}
