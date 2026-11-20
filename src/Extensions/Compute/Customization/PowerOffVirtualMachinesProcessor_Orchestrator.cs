// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Adds resource-id addressing to <see cref="PowerOffVirtualMachinesProcessor"/>. When one or more ids are
    /// supplied they replace <c>VmName</c>/<c>ResourceGroupName</c>/<c>SubscriptionId</c> and each VM
    /// is powered off in turn; otherwise the base single-VM behavior runs unchanged.
    /// </summary>
    public class PowerOffVirtualMachinesProcessor_Orchestrator : PowerOffVirtualMachinesProcessor
    {
        public string[]? Ids { get; set; }

        public override async Task ProcessAsync()
        {
            if (!VirtualMachineIdResolver.HasIds(Ids, VmName, ResourceGroupName))
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

                    using HttpResponseMessage _ = await CreateExtendedProcessor<PowerOffVirtualMachinesProcessor>(p =>
                    {
                        p.SubscriptionId = vmId.SubscriptionId;
                        p.ResourceGroupName = vmId.ResourceGroupName;
                        p.VmName = vmId.Name;
                        p.SkipShutdown = SkipShutdown;
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
