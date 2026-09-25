// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Backs <c>Stop-AzVM</c>, whose single verb spans two REST operations. By default a stop
    /// deallocates the virtual machine so it stops accruing compute charges;
    /// <see cref="StayProvisioned"/> switches to a power off that keeps the machine allocated.
    /// When ids are supplied they replace <c>Name</c>/<c>ResourceGroupName</c>/<c>SubscriptionId</c>
    /// and each VM is stopped in turn.
    /// </summary>
    public class StopVMProcessor_Orchestrator : ComputeProcessorBase<object, object>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string[]? Ids { get; set; }

        /// <summary>Keeps the virtual machine provisioned, selecting power off instead of deallocate.</summary>
        public bool StayProvisioned { get; set; }

        /// <summary>Requests a non-graceful shutdown. Only meaningful alongside <see cref="StayProvisioned"/>.</summary>
        public bool SkipShutdown { get; set; }

        /// <summary>Hibernates the virtual machine as part of the deallocate.</summary>
        public bool Hibernate { get; set; }

        /// <summary>Forces the deallocate. Ignored when <see cref="Hibernate"/> is set.</summary>
        public bool ForceDeallocate { get; set; }

        internal override Task<HttpResponseMessage> SendAsync() =>
            StopVirtualMachineAsync(SubscriptionId, ResourceGroupName, Name);

        public override async Task ProcessAsync()
        {
            if (Ids is null || Ids.Length == 0)
            {
                await base.ProcessAsync();
                return;
            }

            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                foreach (var id in Ids)
                {
                    var vmId = VirtualMachineIdResolver.ParseVirtualMachineResourceId(id);

                    using HttpResponseMessage _ = await StopVirtualMachineAsync(
                        vmId.SubscriptionId,
                        vmId.ResourceGroupName,
                        vmId.Name);
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private Task<HttpResponseMessage> StopVirtualMachineAsync(string subscriptionId, string resourceGroupName, string name)
        {
            if (StayProvisioned)
            {
                return CreateExtendedProcessor<PowerOffVMProcessor>(p =>
                {
                    p.SubscriptionId = subscriptionId;
                    p.ResourceGroupName = resourceGroupName;
                    p.Name = name;
                    p.SkipShutdown = SkipShutdown;
                }).SendAsync();
            }

            return CreateExtendedProcessor<DeallocateVMProcessor>(p =>
            {
                p.SubscriptionId = subscriptionId;
                p.ResourceGroupName = resourceGroupName;
                p.Name = name;
                p.Hibernate = Hibernate;
                // Hibernate and forceDeallocate are mutually exclusive on the service side.
                p.ForceDeallocate = !Hibernate && ForceDeallocate;
            }).SendAsync();
        }
    }
}
