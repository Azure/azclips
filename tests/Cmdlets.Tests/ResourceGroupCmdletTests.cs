using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using AccountsModuleInitializer = Microsoft.Azure.Azclips.Cmdlets.Accounts.ModuleInitializer;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class ResourceGroupCmdletTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Get_SelectsRoute_AndForwardsOnlyBoundParameters(bool namedGroup, bool explicitProfile)
    {
        AzclipsServiceProvider.Dispose();
        var session = new Mock<ISession>();
        var engine = new Mock<IEngine>();
        CommandInvocation? captured = null;
        engine.Setup(engine => engine.OpenExecutionSession(
                It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
            .Returns((CommandInvocation invocation, OutputStreams streams, CancellationToken cancellationToken) =>
            {
                captured = invocation;
                return session.Object;
            });

        try
        {
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object);
                services.AddSingleton(Mock.Of<ITelemetryService>());
            });
            new AccountsModuleInitializer().OnImport();
            var state = InitialSessionState.CreateDefault2();
            state.Commands.Add(new SessionStateCmdletEntry("Get-AzResourceGroup", typeof(GetResourceGroupCmdlet), null));
            using var runspace = RunspaceFactory.CreateRunspace(state);
            runspace.Open();
            using var shell = PowerShell.Create();
            shell.Runspace = runspace;
            shell.AddCommand("Get-AzResourceGroup");
            if (namedGroup) shell.AddParameter("Name", "test-rg");
            if (explicitProfile)
            {
                shell.AddParameter("SubscriptionId", "11111111-1111-1111-1111-111111111111");
                shell.AddParameter("Profile", "test-profile");
            }

            Assert.Empty(shell.Invoke());

            Assert.Empty(shell.Streams.Error);
            Assert.NotNull(captured);
            Assert.Equal("Resources", captured.ExtensionName);
            Assert.Equal(namedGroup ? "Get-AzResourceGroup_Get" : "Get-AzResourceGroup_List", captured.CommandName);
            Assert.False(captured.IsTransient);
            Assert.Equal((namedGroup ? 1 : 0) + (explicitProfile ? 2 : 0), captured.Parameters.Count);
            if (namedGroup) Assert.Equal("test-rg", captured.Parameters["Name"]);
            if (explicitProfile)
            {
                Assert.Equal("11111111-1111-1111-1111-111111111111", captured.Parameters["SubscriptionId"]);
                Assert.Equal("test-profile", captured.Parameters["Profile"]);
            }
            session.Verify(session => session.Run(), Times.Once);
            session.Verify(session => session.Dispose(), Times.Once);
            engine.Verify(engine => engine.OpenExecutionSession(
                It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }
}