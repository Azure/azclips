using Core.Abstractions;
using Tests.Common.Stubs;

namespace Cli.Tests;

public class ProfileServiceTests
{
    private readonly StubProfileService _service = new();

    [Fact]
    public async Task ProfileService_ShouldPersistProfile_WhenSaveProfileAsyncCalled()
    {
        var profile = new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" };
        await _service.SaveProfileAsync(profile);

        var result = await _service.GetProfileAsync("dev");
        Assert.NotNull(result);
        Assert.Equal("t1", result.TenantId);

        var all = await _service.GetAllProfilesAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task ProfileService_ShouldUpdateProfile_WhenProfileAlreadyExists()
    {
        await _service.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "old-sub" });
        await _service.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "new-sub" });

        var result = await _service.GetProfileAsync("dev");
        Assert.NotNull(result);
        Assert.Equal("new-sub", result.SubscriptionId);

        var all = await _service.GetAllProfilesAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task ProfileService_ShouldRemoveProfile_WhenProfileExists()
    {
        await _service.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });
        await _service.SaveProfileAsync(new Profile { Name = "prod", TenantId = "t2", SubscriptionId = "s2" });
        await _service.SetActiveProfileAsync("dev");

        var removed = await _service.RemoveProfileAsync("dev");
        Assert.True(removed);
        Assert.Null(await _service.GetProfileAsync("dev"));
        Assert.Null(await _service.GetActiveProfileAsync());
    }

    [Fact]
    public async Task ProfileService_ShouldReturnFalse_WhenRemovingNonExistentProfile()
    {
        var removed = await _service.RemoveProfileAsync("staging");
        Assert.False(removed);
    }

    [Fact]
    public async Task ProfileService_ShouldSetActiveProfile_WhenProfileExists()
    {
        await _service.SaveProfileAsync(new Profile { Name = "dev", TenantId = "t1", SubscriptionId = "s1" });
        await _service.SaveProfileAsync(new Profile { Name = "prod", TenantId = "t2", SubscriptionId = "s2" });

        await _service.SetActiveProfileAsync("prod");
        var active = await _service.GetActiveProfileAsync();
        Assert.NotNull(active);
        Assert.Equal("prod", active.Name);
    }

    [Fact]
    public async Task ProfileService_ShouldThrow_WhenSettingActiveToNonExistentProfile()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetActiveProfileAsync("staging"));
    }

    [Fact]
    public async Task ProfileService_ShouldFindProfile_WhenNameDiffersByCase()
    {
        await _service.SaveProfileAsync(new Profile { Name = "MyDev", TenantId = "t1", SubscriptionId = "s1" });

        var result = await _service.GetProfileAsync("mydev");
        Assert.NotNull(result);
        Assert.Equal("MyDev", result.Name);
    }

    [Fact]
    public async Task ProfileService_ShouldReturnNull_WhenNoActiveProfile()
    {
        var active = await _service.GetActiveProfileAsync();
        Assert.Null(active);
    }

    [Fact]
    public async Task ProfileService_ShouldReturnEmptyList_WhenNoProfiles()
    {
        var all = await _service.GetAllProfilesAsync();
        Assert.Empty(all);
    }
}
