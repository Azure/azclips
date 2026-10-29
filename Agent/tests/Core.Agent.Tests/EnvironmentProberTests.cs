using Core.Abstractions;
using Core.Agent.Tools.Env;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Profile read failures must not be mistaken for a signed-out account.
/// </summary>
public class EnvironmentProberTests
{
    [Fact]
    public async Task ProfileService_Throws_ProberDoesNotReportSignedOut()
    {
        var prober = new EnvironmentProber(new ThrowingProfileService(), flavor: "azure cli");

        await Assert.ThrowsAsync<InvalidOperationException>(() => prober.ProbeAsync());
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
