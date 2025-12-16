using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for ExtensionDispatcher - the core router that loads extension assemblies
/// and dispatches commands to them.
/// </summary>
public class ExtensionDispatcherTests
{
    [Fact]
    public void Constructor_ShouldUseDefaultPath()
    {
        // Arrange & Act
        var dispatcher = new ExtensionDispatcher();
        
        // Assert
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "..");
        Assert.Equal(expectedPath, dispatcher.ExtensionDirectory);
    }

    [Fact]
    public void Constructor_WithPath_ShouldUseProvidedPath()
    {
        // Arrange
        var expectedPath = "/explicit/path";
        
        // Act
        var dispatcher = new ExtensionDispatcher(expectedPath);
        
        // Assert
        Assert.Equal(expectedPath, dispatcher.ExtensionDirectory);
    }

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
    public void Parse_ShouldThrowKeyNotFoundException_WhenCommandGroupNotRegistered()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var dispatcher = new ExtensionDispatcher(tempDir);
            var args = new[] { "nonexistent", "command" };
            
            // Act & Assert
            var exception = Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => dispatcher.Parse(args));
            Assert.Contains("Command group 'Nonexistent' not found", exception.Message);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
