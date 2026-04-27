using Core.Abstractions;
using Tests.Common.Stubs;

namespace Cli.Tests;

public class ProfileCommandTests
{
    private readonly StubProfileService _profileService = new();

    [Fact]
    public async Task ListProfilesCommand_ShouldReturnProfiles_WhenProfilesExist()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });
        await _profileService.SaveProfileAsync(new Profile { Name = "prod", TenantId = "t2", SubscriptionId = "s2" });
        await _profileService.SetActiveProfileAsync("dev");

        var profiles = await _profileService.GetAllProfilesAsync();
        var active = await _profileService.GetActiveProfileAsync();

        Assert.Equal(2, profiles.Count);
        Assert.NotNull(active);
        Assert.Equal("dev", active.Name);
    }

    [Fact]
    public async Task ShowProfileCommand_ShouldReturnProfile_WhenProfileExists()
    {
        await _profileService.SaveProfileAsync(new Profile
        {
            Name = "dev",
            TenantId = "t1",
            SubscriptionId = "s1",
            Defaults = new Dictionary<string, string> { ["location"] = "eastus2" }
        });

        var profile = await _profileService.GetProfileAsync("dev");
        Assert.NotNull(profile);
        Assert.Equal("t1", profile.TenantId);
        Assert.Equal("eastus2", profile.Defaults["location"]);
    }

    [Fact]
    public async Task ShowProfileCommand_ShouldReturnNull_WhenProfileNotFound()
    {
        var profile = await _profileService.GetProfileAsync("staging");
        Assert.Null(profile);
    }

    [Fact]
    public async Task SwitchProfileCommand_ShouldSetActive_WhenProfileExists()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });
        await _profileService.SaveProfileAsync(new Profile { Name = "prod", TenantId = "t2", SubscriptionId = "s2" });

        await _profileService.SetActiveProfileAsync("prod");
        var active = await _profileService.GetActiveProfileAsync();
        Assert.Equal("prod", active!.Name);
    }

    [Fact]
    public async Task SwitchProfileCommand_ShouldThrow_WhenProfileNotFound()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _profileService.SetActiveProfileAsync("staging"));
    }

    [Fact]
    public async Task ModifyProfileCommand_ShouldUpdateSubscription_WhenProvided()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "old-sub" });

        var profile = await _profileService.GetProfileAsync("dev");
        Assert.NotNull(profile);
        profile.SubscriptionId = "new-sub";
        await _profileService.SaveProfileAsync(profile);

        var updated = await _profileService.GetProfileAsync("dev");
        Assert.Equal("new-sub", updated!.SubscriptionId);
    }

    [Fact]
    public async Task ModifyProfileCommand_ShouldAddDefault_WhenSetProvided()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });

        var profile = await _profileService.GetProfileAsync("dev");
        Assert.NotNull(profile);
        profile.Defaults["location"] = "eastus2";
        await _profileService.SaveProfileAsync(profile);

        var updated = await _profileService.GetProfileAsync("dev");
        Assert.Equal("eastus2", updated!.Defaults["location"]);
    }

    [Fact]
    public async Task RemoveProfileCommand_ShouldDeleteProfile_WhenExists()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });

        var removed = await _profileService.RemoveProfileAsync("dev");
        Assert.True(removed);
        Assert.Null(await _profileService.GetProfileAsync("dev"));
    }

    [Fact]
    public async Task RemoveProfileCommand_ShouldReturnFalse_WhenNotFound()
    {
        var removed = await _profileService.RemoveProfileAsync("staging");
        Assert.False(removed);
    }
}
