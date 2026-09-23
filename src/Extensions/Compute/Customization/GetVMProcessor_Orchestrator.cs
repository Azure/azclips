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

        /// <summary>The one place the switches turn into a request. Mirrors legacy `az vm show`.</summary>
        private string? ResolveExpand() => IncludeUserData ? "userData" : null;

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
    }
}
