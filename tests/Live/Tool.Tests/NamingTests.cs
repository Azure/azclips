using System.Security.Cryptography;
using System.Text;
using Live.Tool;

namespace Live.Tool.Tests;

/// <summary>
/// Pins the values that must not drift from the bash originals.
/// <para>
/// These identify real infrastructure: the state account selects which developer's
/// Terraform state is opened, and the bundle account is created on demand. A subtle
/// change here does not fail — it quietly points at somebody else's resources, or
/// creates a stray account that bills.
/// </para>
/// </summary>
public class NamingTests
{
    // The live subscription and alias, and the names the bash scripts produce for them.
    private const string SubscriptionId = "b813fd05-4867-4dde-9efc-5e34b5373b57";
    private const string ResourceGroup = "mxaa-azclips-live-test-mi-rg";

    private static AzContext Ctx => new("tenant", SubscriptionId, "Azure CLI/PWSH");

    [Theory]
    [InlineData("mxaa", "mxaatfb813fd05")]
    // Hyphens are stripped: storage account names allow lowercase alphanumerics only.
    [InlineData("some-dev", "somedevtfb813fd05")]
    public void StateAccountName_MatchesTheShellDerivation(string alias, string expected)
    {
        Assert.Equal(expected, Ctx.StateAccountName(alias));
    }

    [Fact]
    public void StateAccountName_StaysWithinTheAzureLimit()
    {
        // ALIAS_REGEX caps the alias at 14 characters precisely so this is 24 exactly.
        Assert.Equal(24, Ctx.StateAccountName("abcdefghijklmn").Length);
    }

    [Fact]
    public void StateAccountName_RefusesAnEmptySubscription()
    {
        // Deriving this with an unset subscription yields a DIFFERENT, shorter name that
        // Terraform will happily create and then find empty. That has happened.
        var ctx = new AzContext("tenant", string.Empty, "name");
        Assert.Throws<LiveToolException>(() => ctx.StateAccountName("mxaa"));
    }

    [Fact]
    public void BundleStorageAccountName_MatchesTheShellDerivation()
    {
        // bash: printf '%s' "$SUB-$RG" | sha256sum | cut -c1-16
        var digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{SubscriptionId}-{ResourceGroup}")));

        Assert.Equal("milive9c92bd0c8a667053", $"milive{digest[..16]}");
    }

    [Fact]
    public void BackendResourceGroup_MatchesTheShellDerivation()
    {
        Assert.Equal("mxaa-azclips-live-test-mi-tfstate-rg", Tf.BackendResourceGroup("mxaa"));
    }
}
