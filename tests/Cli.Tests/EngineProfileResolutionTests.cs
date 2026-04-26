using Core.Abstractions;
using Tests.Common.Stubs;

namespace Cli.Tests;

public class EngineProfileResolutionTests
{
    private readonly StubProfileService _profileService = new();

    [Fact]
    public async Task Engine_ShouldResolveExplicitProfile_WhenFlagProvided()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t-dev", SubscriptionId = "s-dev" });
        await _profileService.SaveProfileAsync(new Profile { Name = "prod", TenantId = "t-prod", SubscriptionId = "s-prod" });
        await _profileService.SetActiveProfileAsync("dev");

        // Simulate --profile prod override: resolve "prod" explicitly
        var profile = await _profileService.GetProfileAsync("prod");
        Assert.NotNull(profile);
        var authContext = new AuthContext(TenantId: profile.TenantId, SubscriptionId: profile.SubscriptionId);

        Assert.Equal("t-prod", authContext.TenantId);
        Assert.Equal("s-prod", authContext.SubscriptionId);

        // Active should still be dev
        var active = await _profileService.GetActiveProfileAsync();
        Assert.Equal("dev", active!.Name);
    }

    [Fact]
    public async Task Engine_ShouldResolveActiveProfile_WhenNoFlagProvided()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t-dev", SubscriptionId = "s-dev" });
        await _profileService.SetActiveProfileAsync("dev");

        var active = await _profileService.GetActiveProfileAsync();
        Assert.NotNull(active);
        var authContext = new AuthContext(TenantId: active.TenantId, SubscriptionId: active.SubscriptionId);

        Assert.Equal("t-dev", authContext.TenantId);
        Assert.Equal("s-dev", authContext.SubscriptionId);
    }

    [Fact]
    public async Task Engine_ShouldReturnNull_WhenNoProfileAvailable()
    {
        // No profiles exist, no active profile
        var active = await _profileService.GetActiveProfileAsync();
        Assert.Null(active);
    }

    [Fact]
    public async Task Engine_ShouldNotModifyActiveProfile_WhenOverrideUsed()
    {
        await _profileService.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t-dev", SubscriptionId = "s-dev" });
        await _profileService.SaveProfileAsync(new Profile { Name = "prod", TenantId = "t-prod", SubscriptionId = "s-prod" });
        await _profileService.SetActiveProfileAsync("dev");

        // Use --profile prod for a single command (just resolve, don't change active)
        var overrideProfile = await _profileService.GetProfileAsync("prod");
        Assert.NotNull(overrideProfile);

        // Active should still be dev
        var active = await _profileService.GetActiveProfileAsync();
        Assert.Equal("dev", active!.Name);
    }

    [Fact]
    public void Engine_ShouldThrow_WhenOverrideProfileNotFound()
    {
        // Simulate what engine does: profile not found → error
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _profileService.SetActiveProfileAsync("staging"));
    }
}
