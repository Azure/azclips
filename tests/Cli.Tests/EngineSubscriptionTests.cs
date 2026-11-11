// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using Xunit;

namespace Cli.Tests;

public class EngineSubscriptionTests
{
    [Fact]
    public async Task WarningEvent_ShouldBeRoutedToLoggingServiceWarn()
    {
        var mockLogging = new Mock<ILoggingService>();

        using ISession session = OpenSession(mockLogging);
        await session.EventBus.Publish(Events.Warning, new EventData { Message = "Sample warning message" });

        mockLogging.Verify(l => l.Warn("Sample warning message"), Times.Once);
        mockLogging.Verify(l => l.Info(It.Is<string>(s => s.Contains("Sample warning message"))), Times.Never);
    }

    [Fact]
    public async Task ProgressEvent_ShouldBeRoutedToLoggingServiceVerbose()
    {
        var mockLogging = new Mock<ILoggingService>();

        using ISession session = OpenSession(mockLogging);
        await session.EventBus.Publish(Events.Progress, "Resource type 'name' provisioning status is running");

        mockLogging.Verify(l => l.Verbose("Resource type 'name' provisioning status is running"), Times.Once);
        mockLogging.Verify(l => l.Info(It.Is<string>(s => s.Contains("provisioning status"))), Times.Never);
    }

    private static ISession OpenSession(Mock<ILoggingService> mockLogging)
    {
        var mockConfig = new Mock<IConfigurationService>();
        mockConfig.Setup(c => c.GetConfiguration<BenchmarkOptions>()).Returns(new BenchmarkOptions());

        var engine = new Engine(
            new ClientMetadata(ClientType.CLI, "test-app-id"),
            new Mock<ISendAsyncTerminalFactory>().Object,
            new Mock<IProfileService>().Object,
            new Mock<IAuthService>().Object,
            new Mock<ITelemetryService>().Object,
            mockLogging.Object,
            new Mock<IAssemblyLoadingService>().Object,
            new Mock<IArgumentParsingService>().Object,
            mockConfig.Object,
            new Mock<IHttpPipelineService>().Object,
            new Core.Telemetry.DataBoundaryResolver(new Mock<IPersistenceStore>().Object, new Mock<IProfileService>().Object));

        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("test", "test", new Dictionary<string, object>(), IsTransient: false);
        return engine.OpenExecutionSession(invocation, streams);
    }
}
