using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

public sealed class ProfileModelTests
{
    private static Profile MakeProfile(string? description) => new()
    {
        Name = "prod",
        Description = description,
        TenantId = "tenant-1",
        SubscriptionId = "sub-1",
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    [Fact]
    public void ToString_WithDescription_ShouldRenderDescriptionLine()
    {
        var text = MakeProfile("read-only prod").ToString();

        Assert.Contains("Description: read-only prod", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToString_WithoutMeaningfulDescription_ShouldNotRenderDescriptionLine(string? description)
    {
        // Whitespace-only descriptions (e.g. from a manually edited profiles.json) are treated the
        // same as absent, matching how login normalizes descriptions.
        var text = MakeProfile(description).ToString();

        Assert.DoesNotContain("Description:", text);
    }
}
