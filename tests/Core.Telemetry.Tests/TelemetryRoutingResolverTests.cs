// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Tests.Common.Stubs;

namespace Core.Telemetry.Tests;

public class TelemetryRoutingResolverTests
{
    [Fact]
    public async Task ResolveAsync_PreservesSubscriptionIdAndAuthType_ForManagedIdentityProfile()
    {
        // Managed-identity profiles deliberately do not persist a TenantId (see
        // ManagedIdentityE2ETests.cs), so no boundary lookup can run -- but SubscriptionId/
        // AuthMethod are still populated on the profile and must not be dropped alongside
        // TenantId/Boundary.
        var profileService = new StubProfileService(seedDefault: false);
        await profileService.SaveAndActivateProfileAsync(new Profile
        {
            Name = "default",
            TenantId = "",
            SubscriptionId = "sub-1",
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.ManagedIdentity,
        });

        var (tenantId, boundary, subscriptionId, authType) = await TelemetryRoutingResolver.ResolveAsync(
            profileService,
            new StubAuthService(),
            profileNameOverride: null,
            CancellationToken.None);

        Assert.Null(tenantId);
        Assert.Null(boundary);
        Assert.Equal("sub-1", subscriptionId);
        Assert.Equal(nameof(AuthMethod.ManagedIdentity), authType);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsAllNull_WhenNoActiveProfile()
    {
        var (tenantId, boundary, subscriptionId, authType) = await TelemetryRoutingResolver.ResolveAsync(
            new StubProfileService(seedDefault: false),
            new StubAuthService(),
            profileNameOverride: null,
            CancellationToken.None);

        Assert.Null(tenantId);
        Assert.Null(boundary);
        Assert.Null(subscriptionId);
        Assert.Null(authType);
    }
}
