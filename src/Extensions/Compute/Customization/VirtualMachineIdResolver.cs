// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    internal static class VirtualMachineIdResolver
    {
        internal static (string SubscriptionId, string ResourceGroupName, string Name) ParseVirtualMachineResourceId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Virtual machine resource id cannot be empty.");

            string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 8
                || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
                || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
                || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
                || !segments[5].Equals("Microsoft.Compute", StringComparison.OrdinalIgnoreCase)
                || !segments[6].Equals("virtualMachines", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid virtual machine resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.Compute/virtualMachines/{{vmName}}.");
            }

            return (segments[1], segments[3], segments[7]);
        }
    }
}
