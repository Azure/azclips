// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared detail enrichment behind <c>az vm show -d</c>, <c>az vm list -d</c> and the list forms of
    /// <c>Get-AzVM -Status</c>. Two independent modes, so each caller pays only for what it exposes:
    /// <list type="bullet">
    /// <item><description><see cref="ApplyNetworkAsync"/> resolves NICs and public IPs — <c>--show-details</c> only.</description></item>
    /// <item><description><see cref="ApplyInstanceView"/> flattens the instance view — <c>--show-details</c> and list <c>-Status</c>.</description></item>
    /// </list>
    /// <c>Get-AzVM -Status</c> for one VM uses only <see cref="ResolveExpand"/>, through
    /// <c>GetVirtualMachinesInstanceViewProcessor_Orchestrator</c>, which keeps the instance view intact.
    /// Extracted from <c>GetVirtualMachinesProcessor_Orchestrator</c> so the CLI and PowerShell detail views
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
        internal static async Task ApplyNetworkAsync(ProcessorBase owner, VirtualMachine vm)
        {
            var privateIps = new List<string>();
            var publicIps = new List<string>();
            var fqdns = new List<string>();
            var macAddresses = new List<string>();

            foreach (var nicReference in vm.NetworkProfileNetworkInterfaces ?? [])
            {
                var nicId = VirtualMachineIdResolver.ParseResourceId(
                    nicReference.Id!, "Microsoft.Network", "networkInterfaces", "Network interface", "networkInterfaceName");

                using HttpResponseMessage nicResponse = await owner.CreateChild<GetNetworkInterfacesProcessor>(p =>
                {
                    p.SubscriptionId = nicId.SubscriptionId;
                    p.ResourceGroupName = nicId.ResourceGroupName;
                    p.NetworkInterfaceName = nicId.Name;
                }).SendAsync();

                if (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(owner.Json) is not { } nic)
                {
                    continue;
                }

                Collect(macAddresses, nic.MacAddress);

                foreach (var ipConfiguration in nic.IpConfigurations ?? [])
                {
                    Collect(privateIps, ipConfiguration.PrivateIPAddress);

                    if (ipConfiguration.PublicIpAddressId is not { } publicIpReference)
                    {
                        continue;
                    }

                    var publicIpId = VirtualMachineIdResolver.ParseResourceId(
                        publicIpReference, "Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");

                    using HttpResponseMessage publicIpResponse = await owner.CreateChild<GetPublicIPAddressesProcessor>(p =>
                    {
                        p.SubscriptionId = publicIpId.SubscriptionId;
                        p.ResourceGroupName = publicIpId.ResourceGroupName;
                        p.PublicIpAddressName = publicIpId.Name;
                    }).SendAsync();

                    if (await publicIpResponse.Content.ReadFromJsonAsync<PublicIPAddress>(owner.Json) is not { } publicIp)
                    {
                        continue;
                    }

                    Collect(publicIps, publicIp.IpAddress);
                    Collect(fqdns, publicIp.DnsSettingsFqdn);
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
        /// four fields only the list forms of <c>Get-AzVM -Status</c> surface; <c>--show-details</c>
        /// passes false. <c>Get-AzVM -Status</c> for one VM never comes here: it goes through
        /// <c>GetVirtualMachinesInstanceViewProcessor_Orchestrator</c>, which keeps the instance view intact.
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
