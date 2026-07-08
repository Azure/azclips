// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Completions;

namespace Cli.Tests;

public class CompletionGeneratorTests
{
    [Fact]
    public void BuildCandidateList_ReturnsTopLevelGroups_WhenNoContext()
    {
        var commands = new Dictionary<string, CommandMetadata>
        {
            ["config get"] = CreateCommand("config get"),
            ["login"] = CreateCommand("login"),
            ["config set"] = CreateCommand("config set"),
        };

        var candidates = CompletionGenerator.BuildCandidateList(commands, commandGroupName: null, subcommand: null);

        Assert.Equal(["config", "login"], candidates);
    }

    [Fact]
    public void BuildCandidateList_ReturnsSubcommandsAndGroupParams_WhenGroupContext()
    {
        var commands = new Dictionary<string, CommandMetadata>
        {
            ["config"] = CreateCommand("config", "verbose"),
            ["config get"] = CreateCommand("config get", "key", "json"),
            ["config set"] = CreateCommand("config set", "key", "value"),
        };

        var candidates = CompletionGenerator.BuildCandidateList(commands, "config", subcommand: null);

        Assert.Equal(["get", "set", "--verbose"], candidates);
    }

    [Fact]
    public void BuildCandidateList_ReturnsCommandParams_WhenSubcommandContext()
    {
        var commands = new Dictionary<string, CommandMetadata>
        {
            ["config"] = CreateCommand("config", "verbose"),
            ["config get"] = CreateCommand("config get", "key", "json"),
        };

        var candidates = CompletionGenerator.BuildCandidateList(commands, "config", "get");

        Assert.Equal(["--key", "--json"], candidates);
    }

    [Fact]
    public void BuildCandidateList_FallsBackToGroupParams_WhenSubcommandIsAParameter()
    {
        var commands = new Dictionary<string, CommandMetadata>
        {
            ["login"] = CreateCommand("login", "subscription-id", "tenant-id"),
        };

        var candidates = CompletionGenerator.BuildCandidateList(commands, "login", "--subscription-id");

        Assert.Equal(["--subscription-id", "--tenant-id"], candidates);
    }

    [Fact]
    public void BuildCandidateList_FallsBackToGroupParams_WhenSubcommandIsUnknown()
    {
        var commands = new Dictionary<string, CommandMetadata>
        {
            ["login"] = CreateCommand("login", "subscription-id", "tenant-id"),
        };

        var candidates = CompletionGenerator.BuildCandidateList(commands, "login", "unknown");

        Assert.Equal(["--subscription-id", "--tenant-id"], candidates);
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

    //TODO: rewrite with mocking framework when introduced.
    private static CommandMetadata CreateCommand(string invocationName, params string[] parameters)
    {
        return new CommandMetadata(
            Name: invocationName,
            InvocationName: invocationName,
            Aliases: [],
            FullName: invocationName,
            AssemblyName: "Tests",
            ProcessorType: typeof(CompletionGeneratorTests),
            Parameters: parameters.ToDictionary(
                p => p,
                p => new ParameterMetadata(p, p, typeof(string), string.Empty, IsRequired: false, Aliases: [], AllowedValues: [])),
            ParameterAliases: new Dictionary<string, string>());
    }

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
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
