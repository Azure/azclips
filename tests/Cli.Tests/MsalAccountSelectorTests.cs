// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Authentication;
using Microsoft.Identity.Client;
using Moq;
using Xunit;

namespace Cli.Tests;

// Guards the reported bug where a stale personal account from the shared MSAL cache was used for
// a work profile, causing 403 AuthorizationFailed on ARM. Selection mirrors Azure PowerShell:
// match by UPN, tie-break by tenant, require exactly one match.
public class MsalAccountSelectorTests
{
    private const string WorkTenantId = "544a7a2e-697f-487c-b2b0-a13df7f346b6";
    private const string GuestTenantId = "11111111-2222-3333-4444-555555555555";
    private const string WorkUpn = "alice@contoso.com";
    private const string PersonalUpn = "live.com#alice.test@gmail.com";
    private static IAccount FakeAccount(string username, string tenantId)
    {
        var account = new Mock<IAccount>();
        account.SetupGet(a => a.Username).Returns(username);
        account.SetupGet(a => a.Environment).Returns("login.microsoftonline.com");
        account.SetupGet(a => a.HomeAccountId)
            .Returns(new AccountId($"oid.{tenantId}", "oid", tenantId));
        return account.Object;
    }

    private static Profile ProfileFor(string? upn, string tenantId = WorkTenantId) => new()
    {
        Name = "bami",
        TenantId = tenantId,
        SubscriptionId = "0c302431-1ad0-4187-87c9-d4cebcf7edc2",
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.DeviceCode,
        UserPrincipalName = upn,
    };

    [Fact]
    public void SelectAccount_WithActiveWorkProfile_DoesNotPickStalePersonalAccount()
    {
        // Shared cache holds the personal account first (stale) and the work account second.
        var accounts = new[]
        {
            FakeAccount(PersonalUpn, "personal-tenant"),
            FakeAccount(WorkUpn, WorkTenantId),
        };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(WorkUpn));

        Assert.NotNull(selected);
        Assert.Equal(WorkUpn, selected!.Username);
    }

    [Fact]
    public void SelectAccount_MatchesByUsernameEvenWhenTenantDiffers_GuestTenant()
    {
        // Guest-tenant scenario: the home account's tenant differs from the profile's tenant,
        // but the username matches. PowerShell (EnableGuestTenantAuthentication) still selects it.
        var accounts = new[] { FakeAccount(WorkUpn, WorkTenantId) };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(WorkUpn, GuestTenantId));

        Assert.NotNull(selected);
        Assert.Equal(WorkUpn, selected!.Username);
    }

    [Fact]
    public void SelectAccount_SameUsernameMultipleTenants_DisambiguatesByTenant()
    {
        // Same UPN cached under two tenants; the profile's tenant id breaks the tie.
        var accounts = new[]
        {
            FakeAccount(WorkUpn, GuestTenantId),
            FakeAccount(WorkUpn, WorkTenantId),
        };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(WorkUpn, WorkTenantId));

        Assert.NotNull(selected);
        Assert.Equal(WorkTenantId, selected!.HomeAccountId?.TenantId);
    }

    [Fact]
    public void SelectAccount_NoMatchingAccount_ReturnsNullToForceReauth()
    {
        // Cache only has the personal account; nothing matches the work profile.
        var accounts = new[] { FakeAccount(PersonalUpn, "personal-tenant") };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(WorkUpn));

        Assert.Null(selected);
    }

    [Fact]
    public void SelectAccount_EmptyCache_ReturnsNull()
    {
        var selected = MsalAccountSelector.SelectAccount(System.Array.Empty<IAccount>(), ProfileFor(WorkUpn));

        Assert.Null(selected);
    }

    [Fact]
    public void SelectAccount_NoUpnAndMultipleAccounts_ReturnsNullRatherThanArbitrary()
    {
        // Profile has no stored UPN and the cache is ambiguous: must not pick arbitrarily.
        var accounts = new[]
        {
            FakeAccount(PersonalUpn, "personal-tenant"),
            FakeAccount(WorkUpn, GuestTenantId),
        };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(upn: null, tenantId: WorkTenantId));

        Assert.Null(selected);
    }

    [Fact]
    public void SelectAccount_NoUpnAndSingleAccount_ReturnsNullInsteadOfLoneAccount()
    {
        // Legacy/UPN-less profile with a single cached account. Without a UPN the identity cannot
        // be verified, so the lone (here personal) account must NOT be returned just because it is
        // the only one — that would sign requests with an unrelated identity (the 403 bug).
        var accounts = new[] { FakeAccount(PersonalUpn, "personal-tenant") };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(upn: null, tenantId: WorkTenantId));

        Assert.Null(selected);
    }

    [Fact]
    public void SelectAccount_NoUpnButTenantMatchesOne_ReturnsNullInsteadOfTenantOnlyPick()
    {
        // Empty UPN previously acted as a wildcard, letting selection fall through to a tenant-only
        // match. With the work account's home tenant equal to the profile tenant, the old code would
        // return it based solely on tenantId. UPN is the identity key, tenant is only a tie-break, so
        // an unknown UPN must fail closed rather than select by tenant alone.
        var accounts = new[]
        {
            FakeAccount(PersonalUpn, "personal-tenant"),
            FakeAccount(WorkUpn, WorkTenantId),
        };

        var selected = MsalAccountSelector.SelectAccount(accounts, ProfileFor(upn: null, tenantId: WorkTenantId));

        Assert.Null(selected);
    }
}
