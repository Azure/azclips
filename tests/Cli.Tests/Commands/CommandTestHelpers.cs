// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Cli.Parser;
using Core.Engine;
using Core.Runtime;
using Core.Telemetry;
using Moq;

namespace Cli.Tests.Commands;

/// <summary>
/// Builds a real <see cref="Engine"/> for one extension with every outside dependency mocked, so a test can
/// pass CLI arguments in and check what was sent and written. The real CLI parser, extension index, binder,
/// processor and HTTP pipeline all run; only the wire is a mock.
/// </summary>
internal static class CommandTestHelpers
{
    internal const string TenantId = "11111111-1111-1111-1111-111111111111";
    internal const string SubscriptionId = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// Creates an engine that loads <paramref name="extension"/>, signs in as a fixed profile, and sends every
    /// HTTP request to <paramref name="mockWire"/>. Create the wire mock with <see cref="MockBehavior.Strict"/>
    /// so a request the test did not set up throws <see cref="MockException"/>. With <paramref name="signedIn"/> false there is no active profile.
    /// </summary>
    internal static Engine CreateEngine(Assembly extension, Mock<ISendAsync> mockWire, bool signedIn = true)
    {
        var profile = new Profile { Name = "test", TenantId = TenantId, SubscriptionId = SubscriptionId };
        var mockProfiles = new Mock<IProfileService>();
        mockProfiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync(signedIn ? profile.Name : null);
        mockProfiles.Setup(p => p.GetProfileAsync(profile.Name, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        mockProfiles.Setup(p => p.GetProfileKeysForDefault()).Returns([nameof(Profile.SubscriptionId)]);
        mockProfiles.Setup(p => p.GetProfileValueByKey(It.IsAny<Profile?>(), nameof(Profile.SubscriptionId)))
            .Returns((Profile? p, string _) => p?.SubscriptionId);

        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ReturnsAsync(new AuthResult { AccessToken = "token", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), TenantId = TenantId, Scopes = [] });

        var mockConfiguration = new Mock<IConfigurationService>();
        mockConfiguration.Setup(c => c.GetConfiguration<CoreOptions>()).Returns(new CoreOptions());
        mockConfiguration.Setup(c => c.GetConfiguration<BenchmarkOptions>()).Returns(new BenchmarkOptions());

        var mockLoader = new Mock<IAssemblyLoadingService>();
        mockLoader.Setup(l => l.LoadExtension(It.IsAny<string>(), It.IsAny<string?>())).Returns(extension);

        var mockWireFactory = new Mock<ISendAsyncTerminalFactory>();
        mockWireFactory.Setup(f => f.Create()).Returns(mockWire.Object);

        SendAsyncStep passThrough = (request, callback, next) => next.SendAsync(request, callback);
        var mockPipelineSteps = new Mock<IHttpPipelineService>();
        mockPipelineSteps.SetupGet(s => s.HttpAddUserAgentStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpAddClientRequestIdStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpRequestMessageTracingStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpPipelineTerminatingStep).Returns(passThrough);

        // A cached data boundary for the tenant stops the engine looking it up over the network.
        var mockPersistence = new Mock<IPersistenceStore>();
        mockPersistence.Setup(s => s.ReadAsync(HomePaths.DataBoundaryCacheFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($"{{\"{TenantId}\":\"Global\"}}"));

        var logging = new ConsoleLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging), mockProfiles.Object);

        return new Engine(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"),
            mockWireFactory.Object,
            mockProfiles.Object,
            mockAuth.Object,
            Mock.Of<ITelemetryService>(),
            Mock.Of<ILoggingService>(),
            mockLoader.Object,
            binder,
            mockConfiguration.Object,
            mockPipelineSteps.Object,
            new DataBoundaryResolver(mockPersistence.Object, mockProfiles.Object));
    }

    /// <summary>
    /// Parses <paramref name="commandLine"/> with the real CLI parser into the command to run in
    /// <paramref name="extensionName"/> and its parameters. Every word before the first flag is part of the command name.
    /// </summary>
    internal static CommandInvocation CreateInvocation(string extensionName, string[] commandLine)
    {
        (string command, IDictionary<string, object> parameters) = new ExtensionParser().Parse(commandLine);
        return new CommandInvocation(extensionName, command, parameters);
    }

    /// <summary>Output streams that add everything the command writes to <paramref name="output"/> and ignore its messages.</summary>
    internal static OutputStreams CreateOutput(List<object> output)
    {
        OutputStreamDelegate ignore = (_, _) => Task.CompletedTask;
        return new OutputStreams(
            WriteOutput: (value, _) =>
            {
                output.Add(value);
                return Task.CompletedTask;
            },
            WriteVerbose: ignore,
            WriteDebug: ignore,
            WriteWarning: ignore,
            WriteError: ignore,
            WriteInformation: ignore);
    }
}
