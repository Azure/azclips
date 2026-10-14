// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization
{
    internal static class IdentityIdResolver
    {        
        internal static (string SubscriptionId, string ResourceGroup, string ResourceName) ParseIdentityResourceId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw AzclipsErrorException.InvalidInput("Identity resource id cannot be empty.");

            string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 8
                || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
                || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
                || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
                || !segments[5].Equals("Microsoft.ManagedIdentity", StringComparison.OrdinalIgnoreCase)
                || !segments[6].Equals("userAssignedIdentities", StringComparison.OrdinalIgnoreCase))
            {
                throw AzclipsErrorException.InvalidInput($"Invalid managed identity resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{{resourceName}}.");
            }

            return (segments[1], segments[3], segments[7]);
        }
    }
}
