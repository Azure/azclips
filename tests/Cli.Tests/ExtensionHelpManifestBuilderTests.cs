using Core.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Login;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class ExtensionHelpManifestBuilderTests
{
    [Fact]
    public void BuildManifest_ShouldProduceLoginCommands_WithInlineExamples()
    {
        var index = new ExtensionIndex();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(
            extensionIndex: index,
            extensionName: "login",
            extensionVersion: "1.0.0-alpha");

        Assert.Equal("login", manifest.Extension);
        Assert.Equal("1.0.0-alpha", manifest.Version);
        Assert.Equal(3, manifest.Commands.Count);

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");
        Assert.Equal("Connect-CirrusAccount", loginCommand.PsName);
        Assert.NotEmpty(loginCommand.Summary);
        Assert.NotEmpty(loginCommand.Examples.Cli);

        Assert.Contains(loginCommand.Parameters, p => p.Name == "subscription-id" && p.PsName == "SubscriptionId");
        Assert.Contains(loginCommand.Parameters, p => p.Name == "tenant-id" && p.PsName == "TenantId");
        Assert.Contains(loginCommand.Parameters, p => p.Name == "use-device-code" && p.PsName == "UseDeviceCode");
    }

    [Fact]
    public void BuildIndex_ShouldIncludeGroupAndCommands_FromManifest()
    {
        var index = new ExtensionIndex();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0-alpha");
        var helpIndex = ExtensionHelpManifestBuilder.BuildIndex([manifest]);

        var group = Assert.Single(helpIndex.Groups);
        Assert.Equal("login", group.Name);
        Assert.Equal("login", group.Prefix);

        Assert.Equal(3, helpIndex.Commands.Count);
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login" && c.PsName == "Connect-CirrusAccount");
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login logout" && c.PsName == "Disconnect-CirrusAccount");
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login show" && c.PsName == "Get-CirrusAccount");
    }
}
