using Core.Abstractions;
using Core.Agent.Tools.Env;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="EnvironmentProber"/>. Assert on the snapshot
/// properties the caller consumes.
/// </summary>
public class EnvironmentProberTests
{
    private sealed class FakeProfileService : IProfileService
    {
        private readonly Profile? _profile;
        public FakeProfileService(Profile? profile) => _profile = profile;
        public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Profile>>(_profile is null ? Array.Empty<Profile>() : new[] { _profile });
        public Task<Profile?> GetProfileAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(_profile is not null && _profile.Name == name ? _profile : null);
        public Task SaveProfileAsync(Profile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> RemoveProfileAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetActiveProfileNameAsync(CancellationToken ct = default) => Task.FromResult(_profile?.Name);
        public Task<Profile?> GetActiveProfileAsync(CancellationToken ct = default) => Task.FromResult(_profile);
        public Task SetActiveProfileNameAsync(string? name, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task SignedIn_SnapshotExposesSubAndTenant()
    {
        var profile = new Profile { Name = "default", TenantId = "tenant-1", SubscriptionId = "sub-1", CloudName = "AzureCloud" };
        var prober = new EnvironmentProber(new FakeProfileService(profile), flavor: "azure cli");

        var snap = await prober.ProbeAsync();

        Assert.True(snap.SignedIn);
        Assert.Equal("sub-1", snap.SubscriptionId);
        Assert.Equal("tenant-1", snap.TenantId);
    }

    [Fact]
    public async Task NotSignedIn_SnapshotReportsAbsence()
    {
        var prober = new EnvironmentProber(new FakeProfileService(null), flavor: "azure cli");

        var snap = await prober.ProbeAsync();

        Assert.False(snap.SignedIn);
        Assert.Null(snap.SubscriptionId);
        Assert.Null(snap.TenantId);
    }

    [Fact]
    public async Task ProfileService_Throws_ProberStillReturnsSnapshot()
    {
        var prober = new EnvironmentProber(new ThrowingProfileService(), flavor: "azure cli");

        var snap = await prober.ProbeAsync();

        Assert.False(snap.SignedIn);
    }

    private sealed class ThrowingProfileService : IProfileService
    {
        public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("auth backend down");
        public Task<Profile?> GetProfileAsync(string name, CancellationToken ct = default) =>
            throw new InvalidOperationException("auth backend down");
        public Task SaveProfileAsync(Profile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> RemoveProfileAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetActiveProfileNameAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("auth backend down");
        public Task<Profile?> GetActiveProfileAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("auth backend down");
        public Task SetActiveProfileNameAsync(string? name, CancellationToken ct = default) => Task.CompletedTask;
    }
}
