// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared list orchestrator behind <c>az vm list</c>, <c>Get-AzVM_List</c>,
    /// <c>Get-AzVM_ListByName</c> and <c>Get-AzVM_ListLocation</c>. It owns every
    /// client-facing switch and reduces them to an endpoint choice, an optional ARM <c>$filter</c>,
    /// an exact-match name filter, and a capped per-VM fan-out for the detail switches.
    /// <c>ExtensionIndex</c> decides which switches each client exposes.
    /// </summary>
    public class ListVMProcessor_Orchestrator : ListVMProcessor
    {
        /// <summary>Legacy PowerShell caps the -Status fan-out here to avoid throttling; we cap every detail switch.</summary>
        private const int MaxDetailFanOut = 100;

        /// <summary>Legacy's sentinel for a VM whose detail lookup failed.</summary>
        private const string InfoNotAvailable = "Info Not Available";

        /// <summary>Exact-match name filter, applied client-side. Wildcards are deliberately not supported.</summary>
        public string? Name { get; set; }

        /// <summary>VMSS resource id or VMSS name. A name requires a resource group.</summary>
        public string? Vmss { get; set; }

        public bool ShowDetails { get; set; }
        public bool Status { get; set; }
        public bool IncludeUserData { get; set; }

        /// <summary>True when at least one switch needs a per-VM GET.</summary>
        private bool NeedsDetail => ShowDetails || Status || IncludeUserData;

        /// <summary>True when a switch needs the instance view expanded.</summary>
        private bool NeedsInstanceView => ShowDetails || Status;

        public override async Task ProcessAsync()
        {
            ResolveVmssFilter();
            await base.ProcessAsync();
        }

        /// <summary>
        /// Collects the lazy pages before applying the name filter and detail fan-out,
        /// then writes once. Paging is execution, not an output-hook side effect.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            List<VirtualMachine> virtualMachines = await ReadPagesAsync()
                .CollectItemsAsync(pageBody => pageBody.Value);

            if (!string.IsNullOrWhiteSpace(Name))
            {
                virtualMachines = virtualMachines
                    .Where(virtualMachine => string.Equals(virtualMachine.Name, Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            await EnrichAllAsync(virtualMachines);
            await WriteOutput(virtualMachines);
        }

        /// <summary>
        /// Turns the VMSS switch into the ARM <c>$filter</c> legacy Azure CLI emits. The value is
        /// quoted: the REST spec's description drops the opening quote but keeps the closing one,
        /// so it is an unbalanced documentation typo rather than a different grammar.
        /// </summary>
        private void ResolveVmssFilter()
        {
            if (string.IsNullOrWhiteSpace(Vmss))
            {
                return;
            }

            string vmssId;

            if (Vmss.Contains('/'))
            {
                var parsed = VirtualMachineIdResolver.ParseResourceId(
                    Vmss, "Microsoft.Compute", "virtualMachineScaleSets", "Virtual machine scale set", "vmScaleSetName");

                vmssId = Vmss.Trim();

                // Legacy takes the resource group from the id when it was not supplied separately.
                if (string.IsNullOrWhiteSpace(ResourceGroupName))
                {
                    ResourceGroupName = parsed.ResourceGroupName;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ResourceGroupName))
                {
                    throw AzclipsErrorException.InvalidInput(
                        "Specify the resource group when listing virtual machine instances by scale set name, or pass the scale set resource id instead.");
                }

                vmssId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{Vmss}";
            }

            Filter = $"'virtualMachineScaleSet/id' eq '{vmssId}'";
        }

        /// <summary>
        /// No list endpoint can expand userData, and none returns NIC or public-IP data, so every
        /// detail switch costs one GET per VM. Legacy accepts that cost and so do we; the cap is
        /// what keeps a subscription-wide listing from melting.
        /// </summary>
        private async Task EnrichAllAsync(List<VirtualMachine> virtualMachines)
        {
            if (!NeedsDetail || virtualMachines.Count == 0)
            {
                return;
            }

            string? expand = VirtualMachineDetailEnricher.ResolveExpand(NeedsInstanceView, IncludeUserData);
            int budget = Math.Min(virtualMachines.Count, MaxDetailFanOut);

            for (int index = 0; index < budget; index++)
            {
                virtualMachines[index] = await EnrichOneAsync(virtualMachines[index], expand);
            }

            if (virtualMachines.Count > MaxDetailFanOut)
            {
                await Publisher.Publish(Events.Warning, new EventData
                {
                    Message = $"There are more than {MaxDetailFanOut} virtual machines in the result. Only the details of {MaxDetailFanOut} are shown to avoid throttling. Narrow the result with a resource group or a name to get details for the rest."
                });
            }
        }

        /// <summary>
        /// Re-fetches one VM with the resolved expand and returns the detailed model. A failure is
        /// swallowed the way legacy swallows it — one deleted or erroring VM must not fail the whole
        /// listing — but unlike legacy it is published as a warning so the cause is traceable.
        /// </summary>
        private async Task<VirtualMachine> EnrichOneAsync(VirtualMachine virtualMachine, string? expand)
        {
            try
            {
                var vmId = VirtualMachineIdResolver.ParseVirtualMachineResourceId(virtualMachine.Id);

                using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                {
                    p.SubscriptionId = vmId.SubscriptionId;
                    p.ResourceGroupName = vmId.ResourceGroupName;
                    p.Name = vmId.Name;
                    p.Expand = expand;
                }).SendAsync();

                if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is not { } detailed)
                {
                    virtualMachine.PowerState = InfoNotAvailable;
                    return virtualMachine;
                }

                await ApplyDetailAsync(detailed);
                return detailed;
            }
            catch (Exception exception)
            {
                await Publisher.Publish(Events.Warning, new EventData
                {
                    Message = $"Could not retrieve details for virtual machine '{virtualMachine.Name}': {exception.Message}"
                });

                virtualMachine.PowerState = InfoNotAvailable;
                return virtualMachine;
            }
        }

        /// <summary>
        /// Network enrichment is --show-details only; instance-view flattening serves both switches,
        /// and only -Status asks for the four extra fields.
        /// </summary>
        private async Task ApplyDetailAsync(VirtualMachine virtualMachine)
        {
            if (ShowDetails)
            {
                await VirtualMachineDetailEnricher.ApplyNetworkAsync(this, virtualMachine);
            }

            if (NeedsInstanceView)
            {
                VirtualMachineDetailEnricher.ApplyInstanceView(virtualMachine, includeStatusFields: Status);
            }
        }
    }
}
