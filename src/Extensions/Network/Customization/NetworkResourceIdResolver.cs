// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Turns a name-or-ID input into an ARM resource ID. This builds body data only; it sends no request.
/// </summary>
internal static class NetworkResourceIdResolver
{
    internal const string DdosProtectionPlans = "ddosProtectionPlans";
    internal const string NetworkSecurityGroups = "networkSecurityGroups";
    internal const string RouteTables = "routeTables";
    internal const string NatGateways = "natGateways";
    internal const string ApplicationSecurityGroups = "applicationSecurityGroups";
    internal const string PublicIPAddresses = "publicIPAddresses";

    /// <summary>
    /// A value that starts with '/' is a full ARM resource ID and is returned unchanged, so a
    /// cross-subscription or cross-resource-group ID is kept. Any other value is a name in the
    /// given subscription and resource group.
    /// </summary>
    internal static string ToResourceId(string nameOrId, string subscriptionId, string resourceGroupName, string resourceType)
        => IsResourceId(nameOrId)
            ? nameOrId
            : $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";

    /// <summary>Formats the ID of a subnet in a virtual network of the given subscription and resource group.</summary>
    internal static string ToSubnetId(string subscriptionId, string resourceGroupName, string virtualNetworkName, string subnetName)
        => $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{virtualNetworkName}/subnets/{subnetName}";

    internal static bool IsResourceId(string nameOrId) => nameOrId.StartsWith('/');
}
