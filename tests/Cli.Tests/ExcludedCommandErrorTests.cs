// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.Configuration.Schema;
using Core.DependencyInjection;
using Core.Extensions;
using Core.Runtime;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Tests.Common.Stubs;
using Tests.Common.Utilities;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class ExcludedCommandErrorTests
{
    private const string KnownLeafFailure = "test-only-known-leaf-profile-failure";

    public static TheoryData<string[], string, string, bool, bool> DispatchedCommandFailures
    {
        get
        {
            var cases = new TheoryData<string[], string, string, bool, bool>();
            foreach (var telemetryEnabled in new[] { true, false })
            {
                const string configMissing = "Command 'config nonexistent' not found for client type 'CLI'.";
                const string loginMissing = "Command 'login logout' not found for client type 'CLI'.";
                const string logoutMissing = "Command 'logout login' not found for client type 'CLI'.";
                cases.Add(["config", "nonexistent"], "config nonexistent", configMissing, false, telemetryEnabled);
                cases.Add(["config", "nonexistent"], "config", configMissing, true, telemetryEnabled);
                cases.Add(["login", "logout"], "login logout", loginMissing, false, telemetryEnabled);
                cases.Add(["login", "logout"], "login", loginMissing, true, telemetryEnabled);
                cases.Add(["logout", "login"], "logout login", logoutMissing, false, telemetryEnabled);
                cases.Add(["logout", "login"], "logout", logoutMissing, true, telemetryEnabled);
                cases.Add(["config", "get", "test-only-key"], "config get", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["config", "get", "test-only-key"], "config", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["config", "get", "test-only-key"], "config get test-only-key", KnownLeafFailure, false, telemetryEnabled);
                cases.Add(["config", "get", "--key", "test-only-key"], "config get", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["config", "get", "--key", "test-only-key"], "config get test-only-key", KnownLeafFailure, false, telemetryEnabled);
                cases.Add(["config", "set", "test-only-key", "test-only-value"], "config set", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["config", "set", "test-only-key", "test-only-value"], "config get", KnownLeafFailure, false, telemetryEnabled);
                cases.Add(["config", "set", "test-only-key", "test-only-value"], "config set test-only-key test-only-value", KnownLeafFailure, false, telemetryEnabled);
                cases.Add(["login"], "login", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["login", "--identity"], "login", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["login", "--tenant-id", "test-only-tenant"], "login test-only-tenant", KnownLeafFailure, false, telemetryEnabled);
                cases.Add(["logout"], "logout", KnownLeafFailure, true, telemetryEnabled);
                cases.Add(["logout", "--output", "json"], "logout", KnownLeafFailure, true, telemetryEnabled);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(DispatchedCommandFailures))]
    public async Task Main_DispatchedCommand_MatchesOnlyRegisteredLeaves(
        string[] args, string exclusions, string expectedError, bool expectExcluded, bool telemetryEnabled)
    {
        var configuration = new Mock<IConfigurationService>();
        configuration.Setup(c => c.GetConfiguration<CoreOptions>(It.IsAny<string?>(), It.IsAny<CoreOptions?>()))
            .Returns(new CoreOptions());
        configuration.Setup(c => c.GetConfiguration<ClientsOptions>(It.IsAny<string?>(), It.IsAny<ClientsOptions?>()))
            .Returns(new ClientsOptions());
        configuration.Setup(c => c.GetConfiguration<BenchmarkOptions>(It.IsAny<string?>(), It.IsAny<BenchmarkOptions?>()))
            .Returns(new BenchmarkOptions());
        configuration.Setup(c => c.GetConfiguration<TelemetryOptions>(It.IsAny<string?>(), It.IsAny<TelemetryOptions?>()))
            .Returns(new TelemetryOptions { Enabled = telemetryEnabled });
        var agentOptions = new AgentOptions { Enabled = true, SessionStyle = "oneshot" };
        configuration.Setup(c => c.GetConfiguration<AgentOptions>(It.IsAny<string?>(), It.IsAny<AgentOptions?>()))
            .Returns(agentOptions);
        configuration.Setup(c => c.ListConfigurationAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["agent.excludedCommands"] = exclusions });
        configuration.Setup(c => c.GetSchema()).Returns(new ConfigurationSchema());

        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        profiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        // This is after the real metadata lookup, before any processor or authentication can run.
        profiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException(KnownLeafFailure));
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var assemblies = new Mock<IAssemblyLoadingService>(MockBehavior.Strict);
        assemblies.Setup(a => a.LoadExtension("Config", It.IsAny<string?>()))
            .Returns(typeof(Microsoft.Azure.Azclips.Extensions.Config.ExtensionIndex).Assembly);
        assemblies.Setup(a => a.LoadExtension("Accounts", It.IsAny<string?>()))
            .Returns(typeof(Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex).Assembly);

        var registry = new ExtensionRegistry();
        foreach (var (packageId, index) in new (string, IExtensionIndex)[]
        {
            ("Microsoft.Azure.Azclips.Extensions.Config", new Microsoft.Azure.Azclips.Extensions.Config.ExtensionIndex()),
            ("Microsoft.Azure.Azclips.Extensions.Accounts", new Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex()),
        })
        {
            var projection = Core.Cli.CliExtensionServiceRegistration.BuildContributionProjection(
                new DiscoveredExtensionInfo(
                    PackageId: packageId,
                    ResolvedVersion: string.Empty,
                    Verbs: [],
                    Commands: index.GetCommandTable(ClientType.CLI),
                    Location: null));
            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Version = string.Empty,
                Contributions = projection.Contributions,
                Namespaces = projection.Namespaces,
            };
        }
        var leafIndexBuilds = 0;
        var dispatcher = new ExtensionDispatcher(
            new ExtensionResolver(new InMemoryRegistryStore(registry)),
            () =>
            {
                leafIndexBuilds++;
                return LeafIndex.Build(registry);
            });

        using var source = new ActivitySource("cli-command-identity-" + Guid.NewGuid().ToString("N"));
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
        var telemetryProvider = new Mock<ITelemetryProvider>();
        telemetryProvider.SetupGet(p => p.ActivitySource).Returns(source);
        telemetryProvider.SetupGet(p => p.TracerProvider).Returns(tracerProvider);
        using var telemetry = new TelemetryService(
            telemetryProvider.Object, configuration.Object,
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        var logger = new ScopedLoggingService();
        using var engine = new Core.Engine.Engine(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"),
            new Mock<ISendAsyncTerminalFactory>(MockBehavior.Strict).Object,
            profiles.Object, auth.Object, telemetry, logger, assemblies.Object,
            new Mock<IArgumentParsingService>(MockBehavior.Strict).Object,
            configuration.Object, new Mock<IHttpPipelineService>(MockBehavior.Strict).Object);

        var testHome = Directory.CreateTempSubdirectory("azclips-command-identity-").FullName;
        var previousHome = Environment.GetEnvironmentVariable("AZCLIPS_HOME");
        var previousAgentMode = Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE");
        var stderr = Console.Error;
        var stdout = Console.Out;
        using var errors = new StringWriter();
        using var output = new StringWriter();
        AzclipsServiceProvider.Dispose();
        try
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", testHome);
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", "1");
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(configuration.Object);
                services.AddSingleton<IEngine>(engine);
                services.AddSingleton<ILoggingService>(logger);
                services.AddSingleton<ITelemetryService>(telemetry);
                services.AddSingleton(profiles.Object);
                services.AddSingleton(auth.Object);
                services.AddSingleton(dispatcher);
            });
            Console.SetError(errors);
            Console.SetOut(output);

            var result = await Core.Cli.Program.Main(args);

            Assert.Equal(1, result);
            Assert.Equal(1, errors.ToString().Split(expectedError).Length - 1);
            Assert.Empty(output.ToString());
            Assert.DoesNotContain("agent mode unavailable", errors.ToString());
            configuration.Verify(c => c.GetConfiguration<AgentOptions>(
                It.IsAny<string?>(), It.IsAny<AgentOptions?>()), Times.Once);
            configuration.Verify(c => c.ListConfigurationAsync(false, It.IsAny<CancellationToken>()), Times.Once);
            profiles.Verify(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>()),
                expectedError == KnownLeafFailure ? Times.Once() : Times.Never());
            assemblies.Verify(a => a.LoadExtension(It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
            assemblies.Verify(a => a.LoadAllExtensions(), Times.Never);
            Assert.Empty(auth.Invocations);
            Assert.Equal(1, leafIndexBuilds);

            // Both gates return null; inspect the exclusion state even when no Activity exists.
            var excludedFlag = typeof(Core.Cli.Program).GetField(
                "_lastFallbackSkippedByExclusion", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(excludedFlag);
            Assert.Equal(expectExcluded, Assert.IsType<bool>(excludedFlag.GetValue(null)));
            Assert.False(agentOptions.IsInteractive);
            Assert.Equal("1", Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE"));
            Assert.DoesNotContain(activities, activity => activity.OperationName == "CLI.Activity.AiRecovery");
            if (telemetryEnabled && expectExcluded)
            {
                var skipped = Assert.Single(activities, activity =>
                    activity.OperationName == "CLI.Activity.AiRecoverySkipped");
                Assert.Equal("excluded", skipped.GetTagItem(TelemetryTags.RecoverySkipReason));
            }
            else
            {
                Assert.DoesNotContain(activities, activity =>
                    activity.OperationName == "CLI.Activity.AiRecoverySkipped");
            }
            if (!telemetryEnabled)
            {
                Assert.Empty(activities);
            }
        }
        finally
        {
            Console.SetError(stderr);
            Console.SetOut(stdout);
            AzclipsServiceProvider.Dispose();
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", previousHome);
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", previousAgentMode);
            Directory.Delete(testHome, recursive: true);
        }
    }

    [Theory]
    [InlineData("http", 1)]
    [InlineData("general", 1)]
    [InlineData("typed", 2)]
    public async Task Main_ExcludedResolvedCommand_PrintsOriginalErrorOnceAndPreservesExitCode(
        string errorKind, int expectedExitCode)
    {
        const string message = "unique-excluded-command-error";
        Exception error = errorKind switch
        {
            "http" => new HttpRequestException(message),
            "typed" => AzclipsErrorException.InvalidInput(message),
            _ => new InvalidOperationException(message),
        };
        var configuration = new Mock<IConfigurationService>();
        configuration.Setup(c => c.GetConfiguration<CoreOptions>(It.IsAny<string?>(), It.IsAny<CoreOptions?>()))
            .Returns(new CoreOptions());
        configuration.Setup(c => c.GetConfiguration<ClientsOptions>(It.IsAny<string?>(), It.IsAny<ClientsOptions?>()))
            .Returns(new ClientsOptions());
        configuration.Setup(c => c.GetConfiguration<TelemetryOptions>(It.IsAny<string?>(), It.IsAny<TelemetryOptions?>()))
            .Returns(new TelemetryOptions { Enabled = false });
        configuration.Setup(c => c.GetConfiguration<AgentOptions>(It.IsAny<string?>(), It.IsAny<AgentOptions?>()))
            .Returns(new AgentOptions { Enabled = true, ExcludedCommands = "profile show" });
        configuration.Setup(c => c.ListConfigurationAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["agent.excludedCommands"] = "profile show" });
        configuration.Setup(c => c.GetSchema()).Returns(new ConfigurationSchema());
        var engine = new Mock<IEngine>();
        engine.Setup(e => e.OpenExecutionSession(
                It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
            .Throws(error);
        var registry = new ExtensionRegistry
        {
            Extensions =
            {
                ["Microsoft.Azure.Azclips.Extensions.Config"] = new ExtensionRegistryEntry
                {
                    Version = "0.0.0",
                    Contributions = { new Contribution { Verb = "profile", Path = "show" } },
                },
            },
        };
        var store = new InMemoryRegistryStore(registry);
        var stderr = Console.Error;
        var stdout = Console.Out;
        using var errors = new StringWriter();
        using var output = new StringWriter();
        AzclipsServiceProvider.Dispose();
        try
        {
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(configuration.Object);
                services.AddSingleton(engine.Object);
                services.AddSingleton<IProfileService, StubProfileService>();
                services.AddSingleton<IAuthService, StubAuthService>();
                services.AddSingleton(new ExtensionDispatcher(new ExtensionResolver(store), () => LeafIndex.Build(registry)));
            });
            Console.SetError(errors);
            Console.SetOut(output);

            var result = await Core.Cli.Program.Main(["profile", "show"]);

            Assert.Equal(expectedExitCode, result);
            engine.Verify(e => e.OpenExecutionSession(
                It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, errors.ToString().Split(message).Length - 1);
            Assert.DoesNotContain("agent mode unavailable", errors.ToString());
            Assert.DoesNotContain(message, output.ToString());
        }
        finally
        {
            Console.SetError(stderr);
            Console.SetOut(stdout);
            AzclipsServiceProvider.Dispose();
        }
    }
}
