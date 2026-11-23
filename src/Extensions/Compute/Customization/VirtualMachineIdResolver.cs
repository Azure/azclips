// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Parses ARM resource ids shaped
    /// <c>/subscriptions/{s}/resourceGroups/{g}/providers/{provider}/{type}/{name}</c>. Shared by
    /// --ids / -Id addressing and by the network lookups behind --show-details, so all of them
    /// validate identically and fail with the same wording.
    /// </summary>
    internal static class VirtualMachineIdResolver
    {
        /// <summary>
        /// Enforces that a VM is addressed either by ids or by name and resource group, never both
        /// and never neither. Returns true when ids should be used.
        /// </summary>
        internal static bool HasIds(string[]? ids, string? name, string? resourceGroup)
        {
            bool hasIds = ids is { Length: > 0 };
            bool hasName = !string.IsNullOrWhiteSpace(name);
            bool hasResourceGroup = !string.IsNullOrWhiteSpace(resourceGroup);

            if (hasIds && (hasName || hasResourceGroup))
            {
                throw AzclipsErrorException.InvalidInput(
                    "Ids cannot be combined with Name or ResourceGroupName. Provide either Ids, or both Name and ResourceGroupName.");
            }

            if (!hasIds && !(hasName && hasResourceGroup))
            {
                throw AzclipsErrorException.InvalidInput(
                    "A virtual machine identifier is required. Provide Ids, or both Name and ResourceGroupName.");
            }

            return hasIds;
        }
        internal static (string SubscriptionId, string ResourceGroupName, string Name) ParseVirtualMachineResourceId(string id)
            => ParseResourceId(id, "Microsoft.Compute", "virtualMachines", "Virtual machine", "vmName");

        internal static (string SubscriptionId, string ResourceGroupName, string Name) ParseResourceId(
            string id, string provider, string resourceType, string displayName, string nameToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw AzclipsErrorException.InvalidInput($"{displayName} resource id cannot be empty.");

            string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 8
                || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
                || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
                || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
                || !segments[5].Equals(provider, StringComparison.OrdinalIgnoreCase)
                || !segments[6].Equals(resourceType, StringComparison.OrdinalIgnoreCase))
            {
                throw AzclipsErrorException.InvalidInput($"Invalid {displayName.ToLowerInvariant()} resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/{provider}/{resourceType}/{{{nameToken}}}.");
            }

            return (segments[1], segments[3], segments[7]);
        }
    }
}
