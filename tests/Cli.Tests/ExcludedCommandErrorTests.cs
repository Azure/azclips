// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.Configuration.Schema;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Common.Stubs;
using Tests.Common.Utilities;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class ExcludedCommandErrorTests
{
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
