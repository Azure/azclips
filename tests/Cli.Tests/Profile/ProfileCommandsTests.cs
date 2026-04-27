using System.Linq;
using System.Text.Json;
using Core.Abstractions;
using Core.Authentication;
using Tests.Common.Stubs;
using Xunit;
using Xunit.Abstractions;

namespace Cli.Tests.Profile;

/// <summary>
/// Tests covering the underlying logic for the new CLI profile commands:
///   - azv2 profile list   -> IAuthContextService.ListProfilesAsync
///   - azv2 profile active -> IAuthContextService.GetActiveProfileAsync
///   - azv2 profile set    -> IAuthContextService.SetActiveProfileAsync
///
/// Each test prints the actual output the command would emit, captured via ITestOutputHelper.
/// </summary>
[Collection("ProfileFilesystem")]
public class ProfileCommandsTests : IDisposable
{
    private readonly AuthContextService _svc;
    private readonly string _prefix;
    private readonly ITestOutputHelper _out;
    private readonly string _activePtr;
    private readonly byte[]? _savedActive;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public ProfileCommandsTests(ITestOutputHelper output)
    {
        _out = output;
        _svc = new AuthContextService(new NullLogger(), new StubTelemetryService());
        _prefix = "tst" + Guid.NewGuid().ToString("N")[..8];
        _activePtr = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ".cirrus", "active-profile");
        _savedActive = File.Exists(_activePtr) ? File.ReadAllBytes(_activePtr) : null;
    }

    private string Name(string suffix) => _prefix + suffix;

    private async Task SeedAsync(string name, string tenant = "t-1", string sub = "s-1")
    {
        await _svc.SaveProfileAsync(name, new AuthContext(TenantId: tenant, SubscriptionId: sub));
    }

    [Fact]
    public async Task ProfileList_ShouldIncludeSavedProfile()
    {
        var name = Name("a");
        await SeedAsync(name);

        var profiles = await _svc.ListProfilesAsync();
        var active = await _svc.GetActiveProfileAsync();

        var rendered = profiles
            .Where(p => p.ProfileName.StartsWith(_prefix))
            .Select(p => new
            {
                p.ProfileName,
                p.TenantId,
                p.SubscriptionId,
                IsActive = string.Equals(active?.ProfileName, p.ProfileName, StringComparison.OrdinalIgnoreCase),
            })
            .ToList();

        _out.WriteLine("$ azv2 profile list");
        _out.WriteLine(JsonSerializer.Serialize(rendered, Json));

        Assert.Contains(rendered, p => p.ProfileName == name);
    }

    [Fact]
    public async Task ProfileSet_ShouldUpdateActiveProfile()
    {
        var name = Name("b");
        await SeedAsync(name, tenant: "tenant-b", sub: "sub-b");

        await _svc.SetActiveProfileAsync(name);
        var active = await _svc.GetActiveProfileAsync();

        _out.WriteLine($"$ azv2 profile set --name {name}");
        _out.WriteLine(JsonSerializer.Serialize(active, Json));

        Assert.NotNull(active);
        Assert.Equal(name, active!.ProfileName);
        Assert.Equal("tenant-b", active.TenantId);
        Assert.Equal("sub-b", active.SubscriptionId);
    }

    [Fact]
    public async Task ProfileSet_ShouldThrow_WhenProfileMissing()
    {
        var missing = Name("missing");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.SetActiveProfileAsync(missing));

        _out.WriteLine($"$ azv2 profile set --name {missing}");
        _out.WriteLine($"ERROR: {ex.Message}");
    }

    [Fact]
    public async Task ProfileActive_ShouldReturnLastSetProfile()
    {
        var first = Name("c1");
        var second = Name("c2");
        await SeedAsync(first);
        await SeedAsync(second);

        await _svc.SetActiveProfileAsync(first);
        await _svc.SetActiveProfileAsync(second);

        var active = await _svc.GetActiveProfileAsync();

        _out.WriteLine("$ azv2 profile active");
        _out.WriteLine(JsonSerializer.Serialize(active, Json));

        Assert.NotNull(active);
        Assert.Equal(second, active!.ProfileName);
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("bad/name")]
    [InlineData("..")]
    public async Task ProfileSave_ShouldRejectInvalidNames(string invalid)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _svc.SaveProfileAsync(invalid, new AuthContext("t", "s")));

        _out.WriteLine($"$ azv2 profile save --name \"{invalid}\"");
        _out.WriteLine($"ERROR: {ex.Message}");
    }

    [Fact]
    public async Task ProfileDelete_ShouldRemoveProfile()
    {
        var name = Name("d1");
        await SeedAsync(name);

        await _svc.DeleteProfileAsync(name);
        var profiles = await _svc.ListProfilesAsync();

        _out.WriteLine($"$ azv2 profile delete --name {name}");
        _out.WriteLine($"OK: profile removed.");

        Assert.DoesNotContain(profiles, p => p.ProfileName == name);
    }

    [Fact]
    public async Task ProfileDelete_ShouldRequireForce_WhenActive()
    {
        var name = Name("d2");
        await SeedAsync(name);
        await _svc.SetActiveProfileAsync(name);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.DeleteProfileAsync(name, force: false));

        _out.WriteLine($"$ azv2 profile delete --name {name}");
        _out.WriteLine($"ERROR: {ex.Message}");

        // cleanup with force
        await _svc.DeleteProfileAsync(name, force: true);
    }

    [Fact]
    public async Task ProfileDelete_ShouldClearActive_WhenForced()
    {
        var name = Name("d3");
        await SeedAsync(name);
        await _svc.SetActiveProfileAsync(name);

        await _svc.DeleteProfileAsync(name, force: true);
        var active = await _svc.GetActiveProfileAsync();

        _out.WriteLine($"$ azv2 profile delete --name {name} --force");
        _out.WriteLine($"OK: active profile cleared.");

        Assert.Null(active);
    }

    [Fact]
    public async Task ProfileRename_ShouldMoveProfileAndUpdateActive()
    {
        var oldName = Name("r1");
        var newName = Name("r1new");
        await SeedAsync(oldName, tenant: "tenant-r", sub: "sub-r");
        await _svc.SetActiveProfileAsync(oldName);

        var renamed = await _svc.RenameProfileAsync(oldName, newName);
        var active = await _svc.GetActiveProfileAsync();
        var profiles = await _svc.ListProfilesAsync();

        _out.WriteLine($"$ azv2 profile rename --name {oldName} --new-name {newName}");
        _out.WriteLine(JsonSerializer.Serialize(renamed, Json));

        Assert.Equal(newName, renamed.ProfileName);
        Assert.Equal("tenant-r", renamed.TenantId);
        Assert.NotNull(active);
        Assert.Equal(newName, active!.ProfileName);
        Assert.Contains(profiles, p => p.ProfileName == newName);
        Assert.DoesNotContain(profiles, p => p.ProfileName == oldName);
    }

    [Fact]
    public async Task ProfileRename_ShouldThrow_WhenSourceMissing()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.RenameProfileAsync(Name("nope"), Name("target")));

        _out.WriteLine($"$ azv2 profile rename --name {Name("nope")} --new-name {Name("target")}");
        _out.WriteLine($"ERROR: {ex.Message}");
    }

    [Fact]
    public async Task ProfileRename_ShouldThrow_WhenTargetExists()
    {
        var a = Name("rcA");
        var b = Name("rcB");
        await SeedAsync(a);
        await SeedAsync(b);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.RenameProfileAsync(a, b));

        _out.WriteLine($"$ azv2 profile rename --name {a} --new-name {b}");
        _out.WriteLine($"ERROR: {ex.Message}");
    }

    public void Dispose()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ".cirrus", "profiles");
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.EnumerateFiles(dir, _prefix + "*.json"))
            {
                try { File.Delete(f); } catch { /* best-effort cleanup */ }
            }
        }

        // Always restore the original active pointer (or remove it if there wasn't one).
        try
        {
            if (File.Exists(_activePtr)) File.Delete(_activePtr);
            if (_savedActive is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_activePtr)!);
                File.WriteAllBytes(_activePtr, _savedActive);
            }
        }
        catch { /* best-effort cleanup */ }
    }

    private sealed class NullLogger : ILoggingService
    {
        public void Log(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
