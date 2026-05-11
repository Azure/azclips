using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Parser;
using Microsoft.Azure.Cirrus.Extensions.Config;
using Microsoft.Azure.Cirrus.Extensions.Login;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for ExtensionDispatcher - the core router that loads extension assemblies
/// and dispatches commands to them.
/// </summary>
public class ExtensionDispatcherTests
{

    [Fact]
    public void Parse_ShouldThrowArgumentException_WhenArgsEmpty()
    {
        // Arrange
        var dispatcher = new ExtensionDispatcher();
        var emptyArgs = Array.Empty<string>();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => dispatcher.Parse(emptyArgs));
        Assert.Contains("Arguments cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("?")]
    [InlineData("-?")]
    public void Parse_ShouldThrowDispatchHelpException_WhenHelpFlagProvided(string helpFlag)
    {
        // Arrange
        var dispatcher = new ExtensionDispatcher();
        var args = new[] { helpFlag };
        
        // Act & Assert
        Assert.Throws<DispatchHelpException>(() => dispatcher.Parse(args));
    }

    [Fact]
    public void Parse_ShouldThrowRuntimeExtensionHelpException_WhenGroupHelpRequested()
    {
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<RuntimeExtensionHelpException>(() => dispatcher.Parse(["login", "--help"]));

        Assert.Equal("Login", exception.ExtensionName);
        Assert.False(exception.IsCommandHelp);
        Assert.Equal(["login"], exception.Path);
    }

    [Fact]
    public void Parse_ShouldThrowRuntimeExtensionHelpException_WhenHelpSubcommandUsedForCommand()
    {
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<RuntimeExtensionHelpException>(() => dispatcher.Parse(["help", "login", "show"]));

        Assert.Equal("Login", exception.ExtensionName);
        Assert.True(exception.IsCommandHelp);
        Assert.Equal(["login", "show"], exception.Path);
    }

    [Fact]
    public void Parse_ShouldShortCircuitToHelp_BeforeCommandExecutionPath()
    {
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<RuntimeExtensionHelpException>(() => dispatcher.Parse(["login", "--help", "--subscription-id", "missing"]));

        Assert.Equal("Login", exception.ExtensionName);
        Assert.False(exception.IsCommandHelp);
        Assert.Equal(["login"], exception.Path);
    }

    [Fact]
    public void LoginHelpAdapter_ShouldProvideRuntimeCommandHelp()
    {
        var helpProvider = new ExtensionIndexHelpAdapter(new Microsoft.Azure.Cirrus.Extensions.Login.ExtensionIndex(), "Login");

        var groupHelp = helpProvider.GetGroupHelp(["login"]);
        var commandHelp = helpProvider.GetCommandHelp(["login", "show"]);

        Assert.NotNull(groupHelp);
        Assert.NotNull(commandHelp);
        Assert.Equal("login", groupHelp!.GroupPath);
        Assert.Equal("login show", commandHelp!.CommandPath);
    }

    [Fact]
    public void ConfigExtensionIndex_ShouldExposeTranslatedPowerShellParameters()
    {
        var extensionIndex = new Microsoft.Azure.Cirrus.Extensions.Config.ExtensionIndex();

        var getMetadata = extensionIndex.GetCommandMetadata("Get-CirrusConfig", ClientType.PowerShell);
        Assert.Contains("Json", getMetadata.Parameters.Keys);

        var listMetadata = extensionIndex.GetCommandMetadata("Get-CirrusConfigList", ClientType.PowerShell);
        Assert.Contains("All", listMetadata.Parameters.Keys);
        Assert.Contains("Json", listMetadata.Parameters.Keys);
    }
}
