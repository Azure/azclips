// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli;
using Core.Cli.Completions;
using Core.Extensions;
using Moq;

namespace Cli.Tests;

public class CompletionGeneratorTests
{
    [Theory]
    [InlineData("malformed")]
    [InlineData("unsupported")]
    [InlineData("unreadable")]
    public void RegistryFailurePreservesDiscoveredCompletionCommands(string failure)
    {
        Exception error = failure switch
        {
            "malformed" => new System.Text.Json.JsonException("invalid registry JSON"),
            "unsupported" => new InvalidOperationException("unsupported registry schema"),
            _ => new IOException("registry unavailable"),
        };
        var store = new Mock<IExtensionRegistryStore>();
        store.Setup(value => value.Load()).Throws(error);
        var logging = new Mock<ILoggingService>();
        var info = new DiscoveredExtensionInfo("Widget", "1.0.0", ["widget"],
            Commands("widget show").ToDictionary(pair => pair.Key, pair => pair.Value), null);

        var commands = CompletionGenerator.SelectCommands([info], store.Object, logging.Object);

        Assert.Equal("widget show", Assert.Single(commands).Key);
        logging.Verify(value => value.Debug(It.Is<string>(message =>
            message.Contains("failed to load registry preferences") && message.Contains(error.Message))), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionMatchesRoutingForDuplicateVersionsAndPins(bool pinned)
    {
        DiscoveredExtensionInfo Info(string source, string version, string command)
        {
            var handle = new ExtensionInstallation("Widget", version, $"{source}.dll", source,
                source == "Bundled" ? 0 : 1, 0, source,
                () => throw new InvalidOperationException("Must not load"), () => { });
            return new DiscoveredExtensionInfo("Widget", version, ["widget"], Commands(command)
                .ToDictionary(pair => pair.Key, pair => pair.Value), handle.Location) { AssemblyReference = handle };
        }
        var bundled = Info("Bundled", "2.0.0", "widget bundled");
        var installed = Info("Installed", pinned ? "1.0.0" : "2.0.0", "widget aaa");
        var preferences = new ExtensionRegistry();
        preferences.Extensions["Widget"] = new ExtensionRegistryEntry { Version = "1.0.0", Pinned = pinned };
        var commands = CompletionGenerator.SelectCommands([installed, bundled], preferences, new Mock<ILoggingService>().Object);
        Assert.Equal(pinned ? "widget aaa" : "widget bundled", Assert.Single(commands).Key);
        var selection = ExtensionCandidateSelector.Select([installed, bundled], preferences.Extensions["Widget"]);
        Assert.Same(Assert.Single(selection.Selected.Commands).Value, Assert.Single(commands).Value);
    }

    [Fact]
    public void ResolveEnabled_ReturnsTrue_WhenCompletionEnabled()
    {
        var config = new StubConfigurationService(new CompletionOptions { Enabled = true });

        Assert.True(CompletionGenerator.ResolveEnabled(config));
    }

    [Fact]
    public void ResolveEnabled_ReturnsFalse_WhenCompletionDisabled()
    {
        var config = new StubConfigurationService(new CompletionOptions { Enabled = false });

        Assert.False(CompletionGenerator.ResolveEnabled(config));
    }

    [Fact]
    public void ResolveEnabled_FailsSafeToEnabled_WhenConfigThrows()
    {
        var config = new ThrowingConfigurationService();

        // A benign, default-on convenience feature must not break because the
        // config file is temporarily unreadable — fail safe to enabled.
        Assert.True(CompletionGenerator.ResolveEnabled(config));
    }

    [Fact]
    public void ComputeCandidates_ReturnsNextToken_ForLeadingVerb()
    {
        // `az deployment <tab>` — offer the next token of the deeper commands.
        var commands = Commands("deployment group create", "deployment group what-if");

        var result = CompletionGenerator.ComputeCandidates(commands, ["deployment"]);

        // Both commands share the same next token, so it is deduplicated.
        Assert.Equal(["group"], result);
    }

    [Fact]
    public void ComputeCandidates_ReturnsSubcommands_ForIntermediateGroup()
    {
        // `az deployment group <tab>` — the reported bug: expect create + what-if.
        var commands = Commands("deployment group create", "deployment group what-if");

        var result = CompletionGenerator.ComputeCandidates(commands, ["deployment", "group"]);

        Assert.Equal(["create", "what-if"], result);
    }

    [Fact]
    public void ComputeCandidates_ReturnsParameterFlags_ForExactCommand()
    {
        // `az deployment group create <tab>` — the path is a complete command.
        var commands = new[]
        {
            Command("deployment group create", "name", "resource-group", "template-file"),
        };

        var result = CompletionGenerator.ComputeCandidates(commands, ["deployment", "group", "create"]);

        Assert.Equal(["--name", "--resource-group", "--template-file"], result);
    }

    [Fact]
    public void ComputeCandidates_ReturnsSiblingSubcommands_SortedAndDeduplicated()
    {
        // `az group <tab>` over a 2-level verb.
        var commands = Commands("group show", "group create", "group list", "group create");

        var result = CompletionGenerator.ComputeCandidates(commands, ["group"]);

        Assert.Equal(["create", "list", "show"], result);
    }

    [Fact]
    public void ComputeCandidates_ReturnsEmpty_WhenPathDoesNotMatchAnyCommand()
    {
        var commands = Commands("deployment group create");

        var result = CompletionGenerator.ComputeCandidates(commands, ["deployment", "nope"]);

        Assert.Empty(result);
    }

    private static KeyValuePair<string, CommandMetadata>[] Commands(params string[] invocationNames)
        => invocationNames.Select(name => Command(name)).ToArray();

    private static KeyValuePair<string, CommandMetadata> Command(string invocationName, params string[] parameterNames)
    {
        var parameters = parameterNames.ToDictionary(
            p => p,
            p => new ParameterMetadata(p, p, typeof(string), string.Empty, IsRequired: false, Aliases: [], AllowedValues: []));

        var metadata = new CommandMetadata(
            Name: invocationName,
            InvocationName: invocationName,
            Aliases: [],
            FullName: invocationName,
            AssemblyName: "test",
            ProcessorType: typeof(object),
            Parameters: parameters,
            ParameterAliases: new Dictionary<string, string>());

        return new KeyValuePair<string, CommandMetadata>(invocationName, metadata);
    }

    //TODO: rewrite with mocking framework when introduced.
    private sealed class StubConfigurationService(CompletionOptions completion) : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(CompletionOptions))
            {
                return (T)(object)completion;
            }

            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }

    private sealed class ThrowingConfigurationService : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
            => throw new InvalidOperationException("config file contains invalid JSON");

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
