// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Completions;

namespace Cli.Tests.Recovery;

public class TroubleshootCompletionTests
{
    private const string FirstId = "abcdef0123456789abcdef0123456789";
    private const string SecondId = "0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(10));

    [Fact]
    public async Task Values_DefaultToPlainCanonicalIdsOnly()
    {
        var history = new StubHistoryStore(
            new(SecondId, Timestamp.AddMinutes(-1)),
            new(FirstId.ToUpperInvariant(), Timestamp));
        var config = new StubConfigurationService();

        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id"], history, config);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{FirstId}\n{SecondId}{Environment.NewLine}", result.Output);
        Assert.Empty(result.Error);
        Assert.Equal(1, history.IndexReads);
        Assert.Equal(1, config.CompletionReads);
        Assert.Equal(0, config.AgentReads);
        Assert.False(config.Agent.Enabled);
    }

    [Fact]
    public async Task Values_JsonContainsOnlyIdsAndUtcTimestamps()
    {
        var history = new StubHistoryStore(new FailureHistoryEntry(FirstId, Timestamp));

        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id", "--json"], history);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var entry = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(["value", "label", "description"], entry.EnumerateObject().Select(property => property.Name));
        Assert.Equal(FirstId, entry.GetProperty("value").GetString());
        var label = $"{FirstId} ({Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)})";
        Assert.Equal(label, entry.GetProperty("label").GetString());
        Assert.Equal(label, entry.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ABCDEF")]
    [InlineData(FirstId)]
    public async Task Values_FilterHexPrefixesCaseInsensitively(string prefix)
    {
        var history = new StubHistoryStore(new(SecondId, Timestamp), new(FirstId, Timestamp));

        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id", prefix], history);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(FirstId + Environment.NewLine, result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Values_NoMatchingIdsIsSuccessfulEmptyOutput(bool json)
    {
        var args = new List<string> { "--completion-values", "troubleshoot", "--id", "ffff" };
        if (json)
            args.Add("--json");

        var result = await CompleteAsync(args.ToArray(), new StubHistoryStore(new FailureHistoryEntry(FirstId, Timestamp)));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(json ? "[]" + Environment.NewLine : string.Empty, result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Values_EmptyHistoryIsSuccessfulEmptyOutput(bool json)
    {
        var args = new List<string> { "--completion-values", "troubleshoot", "--id", "" };
        if (json)
            args.Add("--json");

        var result = await CompleteAsync(args.ToArray(), new StubHistoryStore());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(json ? "[]" + Environment.NewLine : string.Empty, result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Values_DisabledCompletionDoesNotResolveHistory(bool json)
    {
        var args = new List<string> { "--completion-values", "troubleshoot", "--id" };
        if (json)
            args.Add("--json");
        var config = new StubConfigurationService { Completion = new() { Enabled = false } };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = await CompletionGenerator.TryHandleValuesAsync(
            args.ToArray(), () => CompletionGenerator.ResolveEnabled(config),
            () => throw new InvalidOperationException("History must not be resolved."),
            output, error);

        Assert.Equal(0, result);
        Assert.Equal(json ? "[]" + Environment.NewLine : string.Empty, output.ToString());
        Assert.Empty(error.ToString());
        Assert.Equal(0, config.AgentReads);
    }

    [Fact]
    public async Task Values_IgnoreOtherCommandsWithoutReadingConfigurationOrHistory()
    {
        foreach (var args in new[] { Array.Empty<string>(), new[] { "troubleshoot", "--id", FirstId }, new[] { "--completions" } })
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var result = await CompletionGenerator.TryHandleValuesAsync(
                args, () => throw new InvalidOperationException("Configuration must not be read."),
                () => throw new InvalidOperationException("History must not be resolved."),
                output, error);

            Assert.Null(result);
            Assert.Empty(output.ToString());
            Assert.Empty(error.ToString());
        }
    }

    public static TheoryData<string[]> InvalidRequests => new()
    {
        new[] { "--completion-values" },
        new[] { "--completion-values", "troubleshoot" },
        new[] { "--completion-values", "vm", "--id" },
        new[] { "--completion-values", "troubleshoot", "--list" },
        new[] { "--completion-values", "troubleshoot", "--id=abc" },
        new[] { "--completion-values", "troubleshoot", "--id", "not-hex" },
        new[] { "--completion-values", "troubleshoot", "--id", " abc" },
        new[] { "--completion-values", "troubleshoot", "--id", "abc\n" },
        new[] { "--completion-values", "troubleshoot", "--id", "$(unsafe)" },
        new[] { "--completion-values", "troubleshoot", "--id", new string('a', 33) },
        new[] { "--completion-values", "troubleshoot", "--id", "--json", "abc" },
        new[] { "--completion-values", "troubleshoot", "--id", "--json", "--json" },
        new[] { "--completion-values", "troubleshoot", "--id", "abc", "--json", "extra" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Values_InvalidRequestReturnsUsageErrorWithoutReadingConfigurationOrHistory(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = await CompletionGenerator.TryHandleValuesAsync(
            args, () => throw new InvalidOperationException("Configuration must not be read."),
            () => throw new InvalidOperationException("History must not be resolved."),
            output, error);

        Assert.Equal(2, result);
        Assert.Empty(output.ToString());
        Assert.StartsWith("Invalid completion request.", error.ToString());
        Assert.DoesNotContain("$(unsafe)", error.ToString());
    }

    [Fact]
    public async Task Values_RejectUnsafeIndexEntriesAndDeduplicateCanonicalIds()
    {
        var history = new StubHistoryStore(
            new(FirstId, Timestamp.AddMinutes(-1)),
            new(FirstId.ToUpperInvariant(), Timestamp),
            new(SecondId, Timestamp.AddMinutes(-2)),
            new(Guid.Empty.ToString("N"), Timestamp),
            new(Guid.ParseExact(FirstId, "N").ToString("D"), Timestamp),
            new(FirstId + "\n", Timestamp),
            new(" " + FirstId, Timestamp),
            new("$(unsafe)", Timestamp),
            new(FirstId.Replace('a', '\u0430'), Timestamp),
            new(null!, Timestamp),
            null!);

        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id"], history);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{FirstId}\n{SecondId}{Environment.NewLine}", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData("history")]
    [InlineData("io")]
    [InlineData("unauthorized")]
    [InlineData("security")]
    [InlineData("json")]
    [InlineData("unavailable")]
    [InlineData("cancelled")]
    public async Task Values_IndexReadFailureIsNotSuccessShapedAndDoesNotLeakDiagnosticText(string failure)
    {
        const string sensitiveDiagnostic = "private-command --secret private-value";
        Exception exception = failure switch
        {
            "history" => new FailureHistoryException("CorruptIndex", sensitiveDiagnostic),
            "io" => new IOException(sensitiveDiagnostic),
            "unauthorized" => new UnauthorizedAccessException(sensitiveDiagnostic),
            "security" => new System.Security.SecurityException(sensitiveDiagnostic),
            "json" => new JsonException(sensitiveDiagnostic),
            "cancelled" => new OperationCanceledException(sensitiveDiagnostic),
            _ => new InvalidOperationException(sensitiveDiagnostic),
        };
        var history = new StubHistoryStore { IndexFailure = exception };

        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id", "--json"], history);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.StartsWith("Unable to read the local failure history index", result.Error);
        Assert.DoesNotContain(sensitiveDiagnostic, result.Error);
    }

    [Fact]
    public async Task Values_ReadFreshIndexAndForwardCancellationOnEachRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var history = new StubHistoryStore(new FailureHistoryEntry(FirstId, Timestamp));
        string[] args = ["--completion-values", "troubleshoot", "--id"];

        var first = await CompleteAsync(args, history, cancellationToken: cancellation.Token);
        history.Entries = [new(SecondId, Timestamp)];
        var second = await CompleteAsync(args, history, cancellationToken: cancellation.Token);

        Assert.Equal(FirstId + Environment.NewLine, first.Output);
        Assert.Equal(SecondId + Environment.NewLine, second.Output);
        Assert.Equal(2, history.IndexReads);
        Assert.Equal(cancellation.Token, history.LastCancellationToken);
    }

    [Fact]
    public void StaticCompletion_IncludesIntrinsicRootAndFlagsWithoutExtensionDiscoveryOnEveryRequest()
    {
        for (var request = 0; request < 2; request++)
        {
            var full = CompletionGenerator.CreateCompletionTree([]);
            var scoped = CompletionGenerator.CreateCompletionTree([], "TROUBLESHOOT");
            var command = Assert.IsType<Dictionary<string, object>>(full["troubleshoot"]);

            Assert.Equal(["--id", "--list"], Assert.IsType<List<string>>(command["_params"]));
            Assert.Equal(["--id", "--list"], Assert.IsType<List<string>>(scoped["_params"]));
            Assert.Equal(["troubleshoot"], CompletionGenerator.ComputeCandidates([], []));
            Assert.Equal(["--id", "--list"], CompletionGenerator.ComputeCandidates([], ["TROUBLESHOOT"]));
            Assert.Empty(CompletionGenerator.CreateCompletionTree([], "troubleshoot", "unknown"));
            Assert.Empty(CompletionGenerator.ComputeCandidates([], ["troubleshoot", "unknown"]));
        }
    }

    [Fact]
    public void StaticCompletion_PreservesUnrelatedScopedShape()
    {
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("name", "Name", typeof(string), string.Empty, IsRequired: true, Aliases: [], AllowedValues: []),
            ["mode"] = new("mode", "Mode", typeof(string), string.Empty, IsRequired: false, Aliases: [], AllowedValues: ["one", "two"]),
        };
        var metadata = new CommandMetadata(
            Name: "create", InvocationName: "deployment group create", Aliases: [],
            FullName: "deployment group create", AssemblyName: "test", ProcessorType: typeof(object),
            Parameters: parameters, ParameterAliases: new Dictionary<string, string>());
        var commands = new[] { new KeyValuePair<string, CommandMetadata>("deployment group create", metadata) };

        var scoped = CompletionGenerator.CreateCompletionTree(commands, "deployment");
        var full = CompletionGenerator.CreateCompletionTree(commands);

        Assert.Equal(["group create"], Assert.IsType<List<string>>(scoped["_subcommands"]));
        Assert.Equal(["--name", "--mode"], Assert.IsType<List<string>>(scoped["group create"]));
        Assert.Equal(["--name"], Assert.IsType<List<string>>(scoped["group create._req"]));
        var values = Assert.IsType<Dictionary<string, object>>(scoped["group create._values"]);
        Assert.Equal(["one", "two"], Assert.IsType<string[]>(values["--mode"]));
        Assert.False(scoped.ContainsKey("_params"));
        Assert.False(scoped.ContainsKey("troubleshoot"));
        Assert.Equal(["deployment", "troubleshoot"], CompletionGenerator.ComputeCandidates(commands, []));
        Assert.Equal(["group"], CompletionGenerator.ComputeCandidates(commands, ["deployment"]));
        Assert.True(full.ContainsKey("deployment"));
        Assert.True(full.ContainsKey("troubleshoot"));
    }

    private static async Task<(int? ExitCode, string Output, string Error)> CompleteAsync(
        string[] args, StubHistoryStore history, StubConfigurationService? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = await CompletionGenerator.TryHandleValuesAsync(
            args, () => CompletionGenerator.ResolveEnabled(config), () => history,
            output, error, cancellationToken);

        Assert.Equal(0, history.RecordReads);
        Assert.Equal(0, history.Writes);
        Assert.Equal(0, history.Keys.Accesses);
        Assert.Equal(0, config.AgentReads);
        return (result, output.ToString(), error.ToString());
    }

    private sealed class StubHistoryStore(params FailureHistoryEntry[] entries) : IFailureHistoryStore
    {
        public IReadOnlyList<FailureHistoryEntry> Entries { get; set; } = entries;
        public Exception? IndexFailure { get; init; }
        public int IndexReads { get; private set; }
        public int RecordReads { get; private set; }
        public int Writes { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public ThrowingKeyStore Keys { get; } = new();

        public Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default)
        {
            IndexReads++;
            LastCancellationToken = cancellationToken;
            return IndexFailure is null
                ? Task.FromResult(Entries)
                : Task.FromException<IReadOnlyList<FailureHistoryEntry>>(IndexFailure);
        }

        public async Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
        {
            RecordReads++;
            await Keys.GetKeyAsync(false, cancellationToken);
            throw new InvalidOperationException("Completion must not read plaintext records.");
        }

        public async Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default)
        {
            Writes++;
            await Keys.GetKeyAsync(true, cancellationToken);
            throw new InvalidOperationException("Completion must not write or prune history.");
        }
    }

    private sealed class ThrowingKeyStore : IFailureHistoryKeyStore
    {
        public int Accesses { get; private set; }
        public Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default)
        {
            Accesses++;
            throw new InvalidOperationException("Completion must not access the key store.");
        }
    }

    private sealed class StubConfigurationService : IConfigurationService
    {
        public CompletionOptions Completion { get; init; } = new() { Enabled = true };
        public AgentOptions Agent { get; } = new() { Enabled = false };
        public int CompletionReads { get; private set; }
        public int AgentReads { get; private set; }

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(CompletionOptions))
            {
                CompletionReads++;
                return (T)(object)Completion;
            }
            if (typeof(T) == typeof(AgentOptions))
            {
                AgentReads++;
                return (T)(object)Agent;
            }
            throw new InvalidOperationException("Unexpected configuration read.");
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
