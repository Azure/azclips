// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
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
            Expand = VirtualMachineDetailEnricher.ResolveExpand(ShowDetails, IncludeUserData);

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
                throw AzclipsErrorException.InvalidInput(
                    "Specify the virtual machine by both name and resource group, or address it by resource id.");
            }
        }

        /// <summary>
        /// `az vm show -d` wants the full network view plus the power state, and never the four
        /// -Status-only fields, so it passes includeStatusFields: false.
        /// </summary>
        private async Task EnrichAsync(VirtualMachine vm)
        {
            await VirtualMachineDetailEnricher.ApplyNetworkAsync(this, vm);
            VirtualMachineDetailEnricher.ApplyInstanceView(vm, includeStatusFields: false);
        }
    }
}
