using System.Text.Json;
using Core.Abstractions;
using Core.Authentication;
using Tests.Common.Stubs;
using Xunit;
using Xunit.Abstractions;

namespace Cli.Tests.Profile;

/// <summary>
/// Verifies legacy <c>context.json</c> migration to a <c>default</c> profile.
/// Touches real <c>~/.local/share/.cirrus/</c> paths because <see cref="AuthContextService"/> hard-codes them today;
/// the test snapshots and restores any pre-existing state.
/// </summary>
[Collection("ProfileFilesystem")]
public class ProfileMigrationTests : IDisposable
{
    private readonly AuthContextService _svc;
    private readonly ITestOutputHelper _out;
    private readonly string _root;
    private readonly string _contextPath;
    private readonly string _backupPath;
    private readonly string _profilesDir;
    private readonly string _defaultProfilePath;
    private readonly string _activePtr;

    private readonly byte[]? _savedContext;
    private readonly byte[]? _savedDefault;
    private readonly byte[]? _savedActive;
    private readonly byte[]? _savedBackup;

    public ProfileMigrationTests(ITestOutputHelper output)
    {
        _out = output;
        _svc = new AuthContextService(new NullLogger(), new StubTelemetryService());
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ".cirrus");
        _contextPath = Path.Combine(_root, "context.json");
        _backupPath = _contextPath + ".migrated.bak";
        _profilesDir = Path.Combine(_root, "profiles");
        _defaultProfilePath = Path.Combine(_profilesDir, "default.json");
        _activePtr = Path.Combine(_root, "active-profile");

        _savedContext = ReadIfExists(_contextPath);
        _savedDefault = ReadIfExists(_defaultProfilePath);
        _savedActive  = ReadIfExists(_activePtr);
        _savedBackup  = ReadIfExists(_backupPath);

        Delete(_contextPath);
        Delete(_defaultProfilePath);
        Delete(_activePtr);
        Delete(_backupPath);
    }

    [Fact]
    public async Task Migration_ShouldMaterializeDefaultProfile_FromLegacyContext()
    {
        Directory.CreateDirectory(_root);
        var legacy = new AuthContext(TenantId: "legacy-tenant", SubscriptionId: "legacy-sub");
        await File.WriteAllTextAsync(_contextPath, JsonSerializer.Serialize(legacy));

        var current = await _svc.GetCurrentContextAsync();

        _out.WriteLine($"After migration: active context = {current}");

        Assert.NotNull(current);
        Assert.Equal("legacy-tenant", current!.TenantId);
        Assert.Equal("legacy-sub", current.SubscriptionId);

        Assert.True(File.Exists(_defaultProfilePath), "default profile file should exist after migration");
        Assert.True(File.Exists(_backupPath), "legacy context.json should be renamed to .migrated.bak");
        Assert.False(File.Exists(_contextPath), "legacy context.json should be removed after migration");
        Assert.True(File.Exists(_activePtr), "active-profile pointer should be written");
        Assert.Equal("default", (await File.ReadAllTextAsync(_activePtr)).Trim());
    }

    [Fact]
    public async Task Migration_ShouldBeIdempotent_OnSubsequentCalls()
    {
        Directory.CreateDirectory(_root);
        var legacy = new AuthContext(TenantId: "legacy-tenant-2", SubscriptionId: "legacy-sub-2");
        await File.WriteAllTextAsync(_contextPath, JsonSerializer.Serialize(legacy));

        await _svc.GetCurrentContextAsync(); // first run migrates
        var snapshotBackup = await File.ReadAllTextAsync(_backupPath);
        var snapshotProfile = await File.ReadAllTextAsync(_defaultProfilePath);

        // Re-create the legacy file to prove a second call does NOT re-migrate (default profile already exists).
        await File.WriteAllTextAsync(_contextPath, JsonSerializer.Serialize(
            new AuthContext("should-not-overwrite", "should-not-overwrite")));

        var current = await _svc.GetCurrentContextAsync();

        _out.WriteLine($"Second call returned: {current}");

        Assert.Equal("legacy-tenant-2", current!.TenantId);
        Assert.Equal(snapshotBackup, await File.ReadAllTextAsync(_backupPath));
        Assert.Equal(snapshotProfile, await File.ReadAllTextAsync(_defaultProfilePath));

        // Cleanup: remove the second legacy file we added.
        Delete(_contextPath);
    }

    [Fact]
    public async Task Migration_ShouldNotRun_WhenProfileStateAlreadyExists()
    {
        Directory.CreateDirectory(_profilesDir);
        var existingProfile = new AuthProfile(
            ProfileName: "default",
            TenantId: "preserved",
            SubscriptionId: "preserved",
            AccountId: "user",
            AccountType: "User",
            Environment: "AzureCloud",
            CreatedAt: DateTimeOffset.UtcNow,
            LastUsedAt: DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(_defaultProfilePath, JsonSerializer.Serialize(existingProfile));
        await File.WriteAllTextAsync(_activePtr, "default");
        await File.WriteAllTextAsync(_contextPath, JsonSerializer.Serialize(
            new AuthContext("legacy-should-be-ignored", "legacy-should-be-ignored")));

        var current = await _svc.GetCurrentContextAsync();

        Assert.Equal("preserved", current!.TenantId);
        Assert.True(File.Exists(_contextPath), "legacy file must remain untouched when profiles already exist");
        Assert.False(File.Exists(_backupPath), "no backup should be written when migration is skipped");
    }

    public void Dispose()
    {
        Delete(_contextPath);
        Delete(_defaultProfilePath);
        Delete(_activePtr);
        Delete(_backupPath);

        Restore(_contextPath, _savedContext);
        Restore(_defaultProfilePath, _savedDefault);
        Restore(_activePtr, _savedActive);
        Restore(_backupPath, _savedBackup);
    }

    private static byte[]? ReadIfExists(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : null;

    private static void Delete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }

    private static void Restore(string path, byte[]? content)
    {
        if (content is null) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, content);
    }

    private sealed class NullLogger : ILoggingService
    {
        public void Log(string m) { }
        public void Info(string m) { }
        public void Verbose(string m) { }
        public void Debug(string m) { }
        public void Warn(string m) { }
        public void Error(string m) { }
    }
}
