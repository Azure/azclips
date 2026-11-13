// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Adds resource-id addressing to <see cref="DeallocateVMProcessor"/>. When one or more ids are
    /// supplied they replace <c>Name</c>/<c>ResourceGroupName</c>/<c>SubscriptionId</c> and each VM
    /// is deallocated in turn; otherwise the base single-VM behavior runs unchanged.
    /// </summary>
    public class DeallocateVMProcessor_Orchestrator : DeallocateVMProcessor
    {
        public string[]? Ids { get; set; }

        public override async Task ProcessAsync()
        {
            if (!VirtualMachineIdResolver.HasIds(Ids, Name, ResourceGroupName))
            {
                await base.ProcessAsync();
                return;
            }

            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                foreach (var id in Ids!)
                {
                    var vmId = VirtualMachineIdResolver.ParseVirtualMachineResourceId(id);

                    using HttpResponseMessage _ = await CreateExtendedProcessor<DeallocateVMProcessor>(p =>
                    {
                        p.SubscriptionId = vmId.SubscriptionId;
                        p.ResourceGroupName = vmId.ResourceGroupName;
                        p.Name = vmId.Name;
                        p.Hibernate = Hibernate;
                        p.ForceDeallocate = ForceDeallocate;
                    }).SendAsync();
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
