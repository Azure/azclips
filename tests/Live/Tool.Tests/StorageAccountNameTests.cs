using Live.Tool;

namespace Live.Tool.Tests;

/// <summary>
/// The state account name has to satisfy Azure's storage naming rules (3-24 lowercase
/// alphanumerics) and stay stable across runs: it holds both the Terraform state and the
/// CLI bundle blobs, and it is derived rather than recorded, so every caller has to
/// arrive at the same string.
/// </summary>
public class StorageAccountNameTests
{
    private static readonly AzContext Ctx = new(
        TenantId: "0b7d2d22-21a7-44c3-975c-eb2c10efb5b6",
        SubscriptionId: "b813fd05-4867-4dde-9efc-5e34b5373b57",
        SubscriptionName: AzContext.ExpectedSubscriptionName);

    [Theory]
    [InlineData("mxaa")]
    [InlineData("some-dev")]
    [InlineData("abcdefghijklmn")]
    public void StateAccountName_IsValidAndStable(string alias)
    {
        var name = Ctx.StateAccountName(alias);

        AssertValidStorageAccountName(name);
        Assert.Equal(name, Ctx.StateAccountName(alias));
    }

    [Fact]
    public void StateAccountName_RefusesAnUnresolvedSubscription()
    {
        var empty = Ctx with { SubscriptionId = string.Empty };

        Assert.Throws<LiveToolException>(() => empty.StateAccountName("mxaa"));
    }

    private static void AssertValidStorageAccountName(string name)
    {
        Assert.InRange(name.Length, 3, 24);
        Assert.All(name, c => Assert.True(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c), $"'{c}' is illegal in '{name}'"));
    }
}
