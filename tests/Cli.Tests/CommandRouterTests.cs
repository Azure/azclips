using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for CommandRouter - the top-level router that maps args[0]
/// to an extension name and passes remaining args through.
/// </summary>
public class CommandRouterTests
{

    [Fact]
    public void Route_ShouldThrowArgumentException_WhenArgsEmpty()
    {
        // Arrange
        var router = new CommandRouter();
        var emptyArgs = Array.Empty<string>();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => router.Route(emptyArgs));
        Assert.Contains("Arguments cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("?")]
    [InlineData("-?")]
    public void Route_ShouldThrowDispatchHelpException_WhenHelpFlagProvided(string helpFlag)
    {
        // Arrange
        var router = new CommandRouter();
        var args = new[] { helpFlag };
        
        // Act & Assert
        Assert.Throws<DispatchHelpException>(() => router.Route(args));
    }
}
