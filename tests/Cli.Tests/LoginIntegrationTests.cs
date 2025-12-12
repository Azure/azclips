using System;
using System.IO;
using Xunit;
using Core.Cli.Parser;
using Core.Implementation;
using Core.Engine;

namespace Cli.Tests;

public class LoginIntegrationTests
{
    [Fact(Skip = "Integration test - requires manual authentication")]
    public void Login_WithDeviceCode_ShouldAuthenticate()
    {
        // Arrange
        var artifactsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "Debug"));
        Environment.SetEnvironmentVariable("AZURE_CLI_EXTENSION_DIRECTORY", artifactsDir);
        
        var dispatcher = new ExtensionDispatcher();
        string[] args = new[] { "login", "--TenantId", "test-tenant-id", "--SubscriptionId", "test-sub-id", "--UseDeviceCode" };
        
        // Act
        var invocation = dispatcher.Parse(args);
        
        // Assert
        Assert.Equal("Login", invocation.ExtensionName);
        Assert.Equal("", invocation.CommandName);
        Assert.Equal("test-tenant-id", invocation.Parameters["TenantId"]);
        Assert.Equal("test-sub-id", invocation.Parameters["SubscriptionId"]);
        Assert.True((bool)invocation.Parameters["UseDeviceCode"]);
    }
}
