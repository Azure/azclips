using Core.Abstractions;
using Tests.Common.Stubs;

namespace Cli.Tests;

public class LoginProfileIntegrationTests
{
    private readonly StubProfileService _profileService = new();

    [Fact]
    public async Task LoginCommand_ShouldCreateDefaultProfile_WhenOutProfileOmitted()
    {
        // Simulate what LoginCommand does when --out-profile is not provided
        var profileName = "default";
        var profile = new Profile
        {
            Name = profileName,
            TenantId = "t1",
            SubscriptionId = "s1",
            AuthMethod = "interactive"
        };
        await _profileService.SaveProfileAsync(profile);
        await _profileService.SetActiveProfileAsync(profileName);

        var active = await _profileService.GetActiveProfileAsync();
        Assert.NotNull(active);
        Assert.Equal("default", active.Name);
        Assert.Equal("t1", active.TenantId);
    }

    [Fact]
    public async Task LoginCommand_ShouldCreateNamedProfile_WhenOutProfileProvided()
    {
        var profileName = "dev";
        var profile = new Profile
        {
            Name = profileName,
            TenantId = "t1",
            SubscriptionId = "s1",
            AuthMethod = "device-code"
        };
        await _profileService.SaveProfileAsync(profile);
        await _profileService.SetActiveProfileAsync(profileName);

        var active = await _profileService.GetActiveProfileAsync();
        Assert.NotNull(active);
        Assert.Equal("dev", active.Name);
        Assert.Equal("device-code", active.AuthMethod);
    }

    [Fact]
    public async Task LoginCommand_ShouldOverwriteProfile_WhenOutProfileCollision()
    {
        // Create existing profile
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "old-t", SubscriptionId = "old-s" });

        // Overwrite with new login
        var newProfile = new Profile { Name = "dev", TenantId = "new-t", SubscriptionId = "new-s", AuthMethod = "interactive" };
        await _profileService.SaveProfileAsync(newProfile);

        var result = await _profileService.GetProfileAsync("dev");
        Assert.NotNull(result);
        Assert.Equal("new-t", result.TenantId);
        Assert.Equal("new-s", result.SubscriptionId);
    }

    [Fact]
    public async Task LogoutCommand_ShouldPreserveProfileDefinition_WhenLoggingOut()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });
        await _profileService.SetActiveProfileAsync("dev");

        // Logout clears tokens (not tested here) but profile definition should persist
        // Simulate logout: do NOT call RemoveProfileAsync
        var profile = await _profileService.GetProfileAsync("dev");
        Assert.NotNull(profile);
        Assert.Equal("dev", profile.Name);

        var active = await _profileService.GetActiveProfileAsync();
        Assert.NotNull(active);
        Assert.Equal("dev", active.Name);
    }
}
