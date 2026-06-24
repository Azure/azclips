using Core.Cli;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Regression: <see cref="Constants.StripCirrusPrefix"/> must use a case-insensitive comparison
/// for all known Microsoft Azure extension package prefixes.
/// Registry keys and NuGet package ids are case-insensitive, so a registry holding e.g.
/// <c>microsoft.azure.cirrus.extensions.compute</c> must still strip cleanly to <c>compute</c>.
/// Pre-fix the strip used <see cref="System.StringComparison.Ordinal"/>, so a lower-cased id
/// passed straight through and the loader re-prepended the prefix.
/// </summary>
public class StripCirrusPrefixTests
{
    [Theory]
    [InlineData("Microsoft.Azure.Azclips.Extensions.Login", "Login")]
    [InlineData("microsoft.azure.azclips.extensions.login", "login")]
    [InlineData("MICROSOFT.AZURE.AZCLIPS.EXTENSIONS.LOGIN", "LOGIN")]
    [InlineData("Microsoft.Azure.Cirrus.Extensions.Compute", "Compute")]
    [InlineData("microsoft.azure.cirrus.extensions.compute", "compute")]
    [InlineData("MICROSOFT.AZURE.CIRRUS.EXTENSIONS.COMPUTE", "COMPUTE")]
    [InlineData("Microsoft.Azure.Cirrus.Extensions.Group", "Group")]
    public void StripCirrusPrefix_ShouldStripCaseInsensitively(string input, string expected)
    {
        Assert.Equal(expected, Constants.StripCirrusPrefix(input));
    }

    [Theory]
    [InlineData("ContosoCorp.MyExtension", "ContosoCorp.MyExtension")]
    [InlineData("Compute", "Compute")] // already short
    [InlineData("Microsoft.Azure.Cirrus.SomethingElse", "Microsoft.Azure.Cirrus.SomethingElse")]
    public void StripCirrusPrefix_ShouldPassThrough_WhenPrefixAbsent(string input, string expected)
    {
        Assert.Equal(expected, Constants.StripCirrusPrefix(input));
    }
}
