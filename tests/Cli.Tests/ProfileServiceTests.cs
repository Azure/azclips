using Core.Abstractions;
using Core.Runtime;
using Xunit;

namespace Cli.Tests;

public sealed class ProfileServiceTests : IDisposable
{
    private const string ValidSubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string SecondValidSubscriptionId = "22222222-2222-2222-2222-222222222222";

    private readonly string _dir;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "azclips-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _service = new ProfileService(new ConsoleLoggingService(), _dir);
    }

    private static Profile MakeProfile(string name, string sub = ValidSubscriptionId, string tenant = "tenant-1") => new()
    {
        Name = name,
        TenantId = tenant,
        SubscriptionId = sub,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    [Fact]
    public async Task SaveAndGet_ShouldRoundTripProfile_AndPersistToProfilesJson()
    {
        await _service.SaveProfileAsync(MakeProfile("prod"));

        var loaded = await _service.GetProfileAsync("prod");
        Assert.NotNull(loaded);
        Assert.Equal("prod", loaded!.Name);
        Assert.Equal(ValidSubscriptionId, loaded.SubscriptionId);
        Assert.True(File.Exists(Path.Combine(_dir, "profiles.json")));
    }

    [Fact]
    public async Task GetProfile_ShouldBeCaseInsensitive()
    {
        await _service.SaveProfileAsync(MakeProfile("Prod"));
        Assert.NotNull(await _service.GetProfileAsync("prod"));
    }

    [Fact]
    public async Task GetAllProfiles_ShouldReturnAllSaved()
    {
        await _service.SaveProfileAsync(MakeProfile("a"));
        await _service.SaveProfileAsync(MakeProfile("b"));

        var all = await _service.GetAllProfilesAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetAllProfiles_ShouldBeEmpty_WhenNoneSaved()
    {
        Assert.Empty(await _service.GetAllProfilesAsync());
    }

    [Fact]
    public async Task GetActiveProfile_ShouldReturnActiveProfile()
    {
        await _service.SaveProfileAsync(MakeProfile("prod", sub: ValidSubscriptionId));
        await _service.SaveProfileAsync(MakeProfile("staging", sub: SecondValidSubscriptionId));
        await _service.SetActiveProfileNameAsync("prod");

        var active = await _service.GetActiveProfileAsync();

        Assert.NotNull(active);
        Assert.Equal("prod", active!.Name);
        Assert.Equal(ValidSubscriptionId, active.SubscriptionId);
    }

    [Fact]
    public async Task GetActiveProfile_ShouldReturnNull_WhenNoActivePointer()
    {
        await _service.SaveProfileAsync(MakeProfile("prod"));

        Assert.Null(await _service.GetActiveProfileAsync());
    }

    [Fact]
    public async Task GetActiveProfile_ShouldReturnNull_WhenActiveProfileNoLongerExists()
    {
        await _service.SetActiveProfileNameAsync("ghost");

        Assert.Null(await _service.GetActiveProfileAsync());
    }

    [Fact]
    public async Task SaveAndActivateProfile_ShouldPersistProfile_AndSetItActive()
    {
        await _service.SaveAndActivateProfileAsync(MakeProfile("prod"));

        Assert.Equal("prod", await _service.GetActiveProfileNameAsync());
        Assert.NotNull(await _service.GetProfileAsync("prod"));
        Assert.True(File.Exists(Path.Combine(_dir, "profiles.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "active-profile")));
    }

    [Fact]
    public async Task SaveAndActivateProfile_ShouldThrow_OnInvalidName()
    {
        await Assert.ThrowsAsync<UsageErrorException>(() => _service.SaveAndActivateProfileAsync(MakeProfile("bad name!")));
    }

    [Fact]
    public async Task SaveAndActivateProfile_ShouldThrow_OnInvalidSubscriptionId()
    {
        var ex = await Assert.ThrowsAsync<UsageErrorException>(() =>
            _service.SaveAndActivateProfileAsync(MakeProfile("prod", sub: "Steven Ma [stema] subscription")));

        Assert.Contains("Invalid subscription id 'Steven Ma [stema] subscription'", ex.Message, StringComparison.Ordinal);
        Assert.Null(await _service.GetProfileAsync("prod"));
        Assert.Null(await _service.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task SetActiveProfile_ShouldPersistSeparately_WithoutTouchingProfilesJson()
    {
        await _service.SetActiveProfileNameAsync("prod");

        Assert.Equal("prod", await _service.GetActiveProfileNameAsync());
        Assert.True(File.Exists(Path.Combine(_dir, "active-profile")));
        Assert.False(File.Exists(Path.Combine(_dir, "profiles.json")));
    }

    [Fact]
    public async Task SetActiveProfile_Null_ShouldClearPointer()
    {
        await _service.SetActiveProfileNameAsync("prod");
        await _service.SetActiveProfileNameAsync(null);

        Assert.Null(await _service.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task RemoveProfile_ShouldReturnFalse_WhenMissing()
    {
        Assert.False(await _service.RemoveProfileAsync("nope"));
    }

    [Fact]
    public async Task RemoveProfile_ShouldDelete_AndLeaveOtherActiveUnchanged()
    {
        await _service.SaveProfileAsync(MakeProfile("prod"));
        await _service.SaveProfileAsync(MakeProfile("staging"));
        await _service.SetActiveProfileNameAsync("prod");

        Assert.True(await _service.RemoveProfileAsync("staging"));
        Assert.Null(await _service.GetProfileAsync("staging"));
        Assert.Equal("prod", await _service.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task RemoveActiveProfile_ShouldClearActivePointer()
    {
        await _service.SaveProfileAsync(MakeProfile("prod"));
        await _service.SetActiveProfileNameAsync("prod");

        Assert.True(await _service.RemoveProfileAsync("prod"));
        Assert.Null(await _service.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task SaveProfile_ShouldThrow_OnInvalidName()
    {
        await Assert.ThrowsAsync<UsageErrorException>(() => _service.SaveProfileAsync(MakeProfile("bad name!")));
    }

    [Fact]
    public async Task SaveProfile_ShouldThrow_OnInvalidSubscriptionId()
    {
        var ex = await Assert.ThrowsAsync<UsageErrorException>(() =>
            _service.SaveProfileAsync(MakeProfile("prod", sub: "Steven Ma [stema] subscription")));

        Assert.Contains("Invalid subscription id 'Steven Ma [stema] subscription'", ex.Message, StringComparison.Ordinal);
        Assert.Null(await _service.GetProfileAsync("prod"));
    }

    [Fact]
    public async Task Save_ShouldNotLeaveTempFiles_BehindAtomicWrite()
    {
        await _service.SaveProfileAsync(MakeProfile("prod"));
        await _service.SetActiveProfileNameAsync("prod");

        var leftover = Directory.GetFiles(_dir, "*.tmp");
        Assert.Empty(leftover);
    }

    [Fact]
    public async Task NoMigration_ShouldIgnoreLegacyContextJson()
    {
        // A pre-existing context.json must not be migrated; the service behaves as if no profiles exist.
        await File.WriteAllTextAsync(Path.Combine(_dir, "context.json"), "{\"TenantId\":\"t\",\"SubscriptionId\":\"s\"}");

        Assert.Empty(await _service.GetAllProfilesAsync());
        Assert.Null(await _service.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task LoadProfiles_ShouldThrowDetailedError_WhenFileIsCorrupt()
    {
        var profilesPath = Path.Combine(_dir, "profiles.json");
        await File.WriteAllTextAsync(profilesPath, "{ this is not valid json");

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetAllProfilesAsync());

        // The message must surface the file path and the underlying cause, not just a generic line.
        Assert.Contains(profilesPath, ex.Message);
        Assert.NotNull(ex.InnerException);
        Assert.Contains(ex.InnerException!.Message, ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
