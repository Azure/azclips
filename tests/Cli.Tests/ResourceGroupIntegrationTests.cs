using System;
using System.IO;
using Xunit;
using Core.Cli.Parser;
using Core.Implementation;

namespace Cli.Tests;

public class ResourceGroupIntegrationTests
{
    [Fact]
    public void ResourceGroupCreate_ShouldParseCorrectly()
    {
        // Arrange
        var artifactsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "Debug"));
        Environment.SetEnvironmentVariable("AZURE_CLI_EXTENSION_DIRECTORY", artifactsDir);
        
        var dispatcher = new ExtensionDispatcher();
        string[] args = new[] { "group", "create", "--SubscriptionId", "test-sub-id", "--Name", "test-rg", "--Location", "eastus" };
        
        // Act
        var invocation = dispatcher.Parse(args);
        
        // Assert
        Assert.Equal("Resources", invocation.ExtensionName);
        Assert.Equal("create", invocation.CommandName);
        Assert.Equal("test-sub-id", invocation.Parameters["SubscriptionId"]);
        Assert.Equal("test-rg", invocation.Parameters["Name"]);
        Assert.Equal("eastus", invocation.Parameters["Location"]);
    }
}
