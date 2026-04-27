using Core.Abstractions;
using System.Text.Json;

namespace Cli.Tests;

public class ProfileModelTests
{
    [Fact]
    public void Profile_ShouldAcceptValidProperties_WhenConstructed()
    {
        var profile = new Profile
        {
            Name = "my-dev",
            TenantId = "t1",
            SubscriptionId = "s1"
        };

        Assert.Equal("my-dev", profile.Name);
        Assert.Equal("t1", profile.TenantId);
        Assert.Equal("s1", profile.SubscriptionId);
        Assert.Equal("AzureCloud", profile.CloudName);
        Assert.Empty(profile.Defaults);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    public void Profile_ShouldThrow_WhenNameContainsInvalidCharacters(string invalidName)
    {
        Assert.Throws<ArgumentException>(() => new Profile { Name = invalidName });
    }

    [Fact]
    public void Profile_ShouldSerializeAndDeserialize_WhenUsingJsonSerializer()
    {
        var profile = new Profile
        {
            Name = "dev",
            TenantId = "t1",
            SubscriptionId = "s1",
            CloudName = "AzureChinaCloud",
            AuthMethod = "interactive",
            UserPrincipalName = "user@example.com",
            ClientId = "app-id",
            Defaults = new Dictionary<string, string>
            {
                ["location"] = "eastus2",
                ["resource-group"] = "dev-rg"
            }
        };

        var json = JsonSerializer.Serialize(profile);
        var deserialized = JsonSerializer.Deserialize<Profile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("dev", deserialized.Name);
        Assert.Equal("t1", deserialized.TenantId);
        Assert.Equal("s1", deserialized.SubscriptionId);
        Assert.Equal("AzureChinaCloud", deserialized.CloudName);
        Assert.Equal("interactive", deserialized.AuthMethod);
        Assert.Equal("user@example.com", deserialized.UserPrincipalName);
        Assert.Equal("app-id", deserialized.ClientId);
        Assert.Equal("eastus2", deserialized.Defaults["location"]);
        Assert.Equal("dev-rg", deserialized.Defaults["resource-group"]);
    }

    [Fact]
    public void Profile_ShouldDisplayReadableToString()
    {
        var profile = new Profile
        {
            Name = "dev",
            TenantId = "t1",
            SubscriptionId = "s1"
        };

        var str = profile.ToString();
        Assert.Contains("Name: dev", str);
        Assert.Contains("Tenant: t1", str);
        Assert.Contains("Subscription: s1", str);
    }
}
