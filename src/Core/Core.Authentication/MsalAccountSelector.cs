// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

// Picks the cached MSAL account matching the active profile.
// Require a stored UPN, match by UPN, tie-break by tenant, require exactly one.
public static class MsalAccountSelector
{
    public static IAccount? SelectAccount(IEnumerable<IAccount> accounts, Profile? profile)
    {
        var username = profile?.UserPrincipalName;
        var tenantId = profile?.TenantId;

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        // match by UPN only, so a home account can still reach its guest tenants
        var filtered = accounts
            .Where(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Same UPN under multiple home tenants: use the tenant to break the tie.
        if (filtered.Count > 1)
        {
            filtered = filtered
                .Where(a => string.IsNullOrWhiteSpace(tenantId)
                            || string.Equals(a.HomeAccountId?.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return filtered.Count == 1 ? filtered[0] : null;
    }
}
