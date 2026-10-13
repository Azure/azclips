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

    [Fact]
    public async Task ResolveAsync_LogsDebugOnCatch_WhenLoggingServiceProvided()
    {
        var throwingProfileService = new ThrowingProfileService();
        var capturingLogger = new CapturingLoggingService();

        var (tenantId, boundary, subscriptionId, authType) = await TelemetryRoutingResolver.ResolveAsync(
            throwingProfileService,
            new StubAuthService(),
            profileNameOverride: "test-profile",
            CancellationToken.None,
            loggingService: capturingLogger);

        Assert.Null(tenantId);
        Assert.Null(boundary);
        Assert.Null(subscriptionId);
        Assert.Null(authType);
        Assert.Contains(capturingLogger.Messages, m => m.Contains("[TelemetryRoutingResolver] Failed to resolve telemetry routing for profile 'test-profile': Simulated failure"));
    }

    private sealed class ThrowingProfileService : StubProfileService
    {
        public ThrowingProfileService() : base(seedDefault: false) { }

        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure");
        public override Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure");
    }

    private sealed class CapturingLoggingService : ILoggingService
    {
        public List<string> Messages { get; } = new();
        public void Log(string message) => Messages.Add($"LOG: {message}");
        public void Info(string message) => Messages.Add($"INFO: {message}");
        public void Verbose(string message) => Messages.Add($"VERBOSE: {message}");
        public void Debug(string message) => Messages.Add($"DEBUG: {message}");
        public void Warn(string message) => Messages.Add($"WARN: {message}");
        public void Error(string message) => Messages.Add($"ERROR: {message}");
    }
}
