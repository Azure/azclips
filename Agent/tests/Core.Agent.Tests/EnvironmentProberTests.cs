using Core.Abstractions;
using Core.Agent.Tools.Env;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="EnvironmentProber"/>. Only the real branch
/// (auth backend throws → probe still returns a snapshot with SignedIn=false)
/// is kept — the signed-in / not-signed-in field-copy assertions were dropped
/// as tautological.
/// </summary>
public class EnvironmentProberTests
{
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
        public List<string> GetProfileKeysForDefault() => throw new NotImplementedException();
        public string? GetProfileValueByKey(Profile? profile, string key) => throw new NotImplementedException();
    }
}
