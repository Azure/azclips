using Core.Abstractions;
using Core.Agent.Tools.Resource.Cache;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Focused tests for <see cref="ArmCacheRefresher"/> that don't require network
/// or a real IAuthService. Full end-to-end behavior (ARG paging, atomic write,
/// lock contention) is exercised by manual runs against a live tenant — those
/// paths can't be unit-tested without an ARM emulator.
/// </summary>
public class ArmCacheRefresherTests
{
    private sealed class NullAuth : IAuthService
    {
        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task LogoutAsync(string? clientId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("no token expected — refresher must return early");
    }

    private sealed class NoActiveProfile : IProfileService
    {
        public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Profile>>(Array.Empty<Profile>());
        public Task<Profile?> GetProfileAsync(string name, CancellationToken ct = default) => Task.FromResult<Profile?>(null);
        public Task SaveProfileAsync(Profile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> RemoveProfileAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetActiveProfileNameAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<Profile?> GetActiveProfileAsync(CancellationToken ct = default) => Task.FromResult<Profile?>(null);
        public Task SetActiveProfileNameAsync(string? name, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task NotSignedIn_RefreshOnceReturnsWithoutTouchingAuth()
    {
        using var tempDir = new TempDirectory();
        var refresher = new ArmCacheRefresher(
            new NullAuth(),
            new NoActiveProfile(),
            http: null,
            cacheRoot: tempDir.Path);

        // Should NOT throw — the not-signed-in early return prevents auth from
        // being called (NullAuth would blow up if it were).
        await refresher.RefreshOnceAsync();
        Assert.Null(refresher.LastError);
        Assert.False(File.Exists(Path.Combine(tempDir.Path, "tenants.json")));
    }

    [Fact]
    public async Task NotSignedIn_KickAsyncSwallowsAndDoesNotThrow()
    {
        using var tempDir = new TempDirectory();
        var refresher = new ArmCacheRefresher(
            new NullAuth(),
            new NoActiveProfile(),
            http: null,
            cacheRoot: tempDir.Path);

        refresher.KickAsync();
        // Give the fire-and-forget task a beat to complete; the not-signed-in
        // path returns synchronously in ~ms after entering the Task.
        await Task.Delay(100);
        Assert.Null(refresher.LastError);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"azclips-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
