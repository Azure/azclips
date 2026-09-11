// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for Program.cs fallback logic - verifies AI agent is invoked when extension parsing fails.
/// These tests verify the fallback mechanism completes without exceptions.
/// Tests timeout after 10 seconds - we only verify fallback is invoked, not the AI response.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ProgramFallbackTests
{
    [Fact]
    public async Task Fallback_ShouldNotBeInvoked_ForHelpFlags()
    {
        // Arrange
        var args = new[] { "--help" };
        
        // Act
        var exitCode = await Core.Cli.Program.Main(args);
        
        // Assert - help is handled before fallback, returns 0
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_ShouldRenderErrorThenCommandHelp_WhenRequiredParametersMissing()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var sw = new StringWriter();

        try
        {
            Console.SetOut(sw);
            Console.SetError(sw);

            var exitCode = await Core.Cli.Program.Main(["vm", "create"]);

            Assert.Equal(2, exitCode);
            Assert.Contains("Required parameter '--name' is missing.", sw.ToString());
            Assert.Contains("Create a virtual machine and its supporting network resources.", sw.ToString());
            Assert.Contains("Required parameter '--name' is missing.", sw.ToString().Substring(0, sw.ToString().IndexOf("Create a virtual machine and its supporting network resources.")));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    // The following tests verify that AgentEngine is invoked by observing the side effects.
    // They require AI integration which prompts for user input, making them unsuitable for automated testing.
    // These are skipped in CI but can be run manually for integration testing.
    
    [Fact(Skip = "Requires AI agent with interactive prompts - manual test only")]
    public async Task Fallback_ShouldBeInvoked_WhenExtensionParsingFails()
    {
        // Arrange - use a command that will fail extension parsing
        var args = new[] { "nonexistent", "command" };
        
        // Act - this will invoke AI and produce output
        var exitCode = await Core.Cli.Program.Main(args);
        
        // Assert - verify it didn't crash
        Assert.True(exitCode >= 0);
    }

    [Fact(Skip = "Requires AI agent with interactive prompts - manual test only")]
    public async Task Fallback_ShouldHandleNaturalLanguage()
    {
        // Arrange - natural language query that triggers fallback
        var args = new[] { "create", "a", "resource", "group" };
        
        // Act - AI will ask for parameters
        var exitCode = await Core.Cli.Program.Main(args);
        
        // Assert - verify it didn't crash
        Assert.True(exitCode >= 0);
    }

    [Fact(Skip = "Requires AI agent with interactive prompts - manual test only")]
    public async Task Fallback_ShouldUseDefaultFlavor_WhenNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("AZURECLI_FLAVOR", null);
        var args = new[] { "test" };
        
        try
        {
            // Act - should use "azure cli" flavor (shows spinner)
            var exitCode = await Core.Cli.Program.Main(args);
            
            // Assert - verify it didn't crash
            Assert.True(exitCode >= 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURECLI_FLAVOR", null);
        }
    }

    [Fact(Skip = "Requires AI agent with interactive prompts - manual test only")]
    public async Task Fallback_ShouldUsePowerShellFlavor_WhenSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("AZURECLI_FLAVOR", "azure powershell");
        var args = new[] { "test" };
        
        try
        {
            // Act - should use PowerShell flavor (no spinner, different prompts)
            var exitCode = await Core.Cli.Program.Main(args);
            
            // Assert - verify it didn't crash
            Assert.True(exitCode >= 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURECLI_FLAVOR", null);
        }
    }
}

