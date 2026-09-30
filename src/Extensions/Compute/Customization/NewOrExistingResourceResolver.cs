// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Legacy Azure CLI's handling of a vm create argument that names a network resource to attach
/// or create (<c>--nsg</c>, <c>--public-ip-address</c>; <c>nsg_type</c> and
/// <c>public_ip_address_type</c> in azure-cli's <c>_validators.py</c>). Omitted means new with the
/// default name, <c>""</c> means none, a name is looked up in the VM's resource group and created
/// when missing, and a resource id must already exist.
/// </summary>
internal static class NewOrExistingResourceResolver
{
    /// <summary>What a resource id of this kind looks like, and how messages name it.</summary>
    internal sealed record ResourceKind(string Provider, string ResourceType, string DisplayName, string NameToken);

    internal static readonly ResourceKind NetworkSecurityGroup =
        new("Microsoft.Network", "networkSecurityGroups", "Network security group", "networkSecurityGroupName");

    internal static readonly ResourceKind PublicIpAddress =
        new("Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");

    /// <summary>
    /// Returns the id of an existing resource to attach, or the name of one to create; both are
    /// null when none is wanted. <paramref name="findIdAsync"/> takes a subscription, resource group
    /// and name, and returns the resource's id, or null when it does not exist.
    /// </summary>
    internal static async Task<(string? ExistingId, string? NameToCreate)> ResolveAsync(
        string? value,
        string defaultName,
        string subscriptionId,
        string resourceGroupName,
        ResourceKind kind,
        Func<string, string, string, Task<string?>> findIdAsync)
    {
        if (value is null)
        {
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                return (null, null);
            }
            return (null, defaultName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        if (value.Contains('/'))
        {
            var parsed = VirtualMachineIdResolver.ParseResourceId(
                value, kind.Provider, kind.ResourceType, kind.DisplayName, kind.NameToken);
            string existingId = await findIdAsync(parsed.SubscriptionId, parsed.ResourceGroupName, parsed.Name)
                ?? throw new InvalidOperationException($"{kind.DisplayName} '{value}' was not found.");
            return (existingId, null);
        }

        string? existingByName = await findIdAsync(subscriptionId, resourceGroupName, value);
        if (existingByName is not null)
        {
            return (existingByName, null);
        }
        return (null, value);
    }
}
