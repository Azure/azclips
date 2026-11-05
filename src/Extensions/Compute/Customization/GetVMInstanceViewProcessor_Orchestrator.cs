// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Read orchestrator behind <c>az vm get-instance-view</c>, and behind <c>Get-AzVM -Status</c> for one
    /// VM (<c>Get-AzVM_GetStatus</c>, <c>Get-AzVM_IdStatus</c>). Every request asks ARM for the instance
    /// view, plus the user data when PowerShell passes <c>-UserData</c>. The VM is written with
    /// <c>properties.instanceView</c> intact: nothing is flattened onto the model and nothing is cleared,
    /// unlike the detail switches of <c>vm show</c> and <c>vm list</c>. Addressed by name and resource
    /// group, or by one or more resource ids, one GET per id, written as one array.
    /// </summary>
    public class GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor
    {
        public string[]? Ids { get; set; }
        public bool IncludeUserData { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: IncludeUserData);

            if (Ids is null || Ids.Length == 0)
            {
                EnsureAddressable();
                await base.ProcessAsync();
                return;
            }

            // Parse every id before the first request, so one malformed id fails the command
            // without reading the others.
            var vmIds = Ids.Select(VirtualMachineIdResolver.ParseVirtualMachineResourceId).ToList();

            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                var results = new List<VirtualMachine>();
                foreach (var vmId in vmIds)
                {
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
    }
}
