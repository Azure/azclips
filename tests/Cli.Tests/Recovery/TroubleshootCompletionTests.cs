// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Completions;
using Moq;

namespace Cli.Tests.Recovery;

public class TroubleshootCompletionTests
{
    private const string FirstId = "42-2026-0924-0000-0002";
    private const string SecondId = "2-2026-0924-0000-0001";
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Values_ReturnCanonicalIdsWithOnlyAnIndexRead()
    {
        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id"],
            [new(SecondId, Timestamp.AddMinutes(-1)), new(FirstId, Timestamp)]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{FirstId}\n{SecondId}{Environment.NewLine}", result.Output);
        Assert.Empty(result.Error);
    }

    public static TheoryData<string[]> JsonValueRequests => new()
    {
        new[] { "--completion-values", "troubleshoot", "--id", "--json" },
        new[] { "--completion-values", "troubleshoot", "show", "--id", "--json" },
        new[] { "--completion-values", "troubleshoot", "--id=42", "--json" },
        new[] { "--completion-values", "troubleshoot", "show", "--id=42", "--json" },
    };

    [Theory]
    [MemberData(nameof(JsonValueRequests))]
    public async Task Values_JsonContainsOnlyIdsAndUtcTimestamps(string[] args)
    {
        var result = await CompleteAsync(args, [new(FirstId, Timestamp)]);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var entry = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(["value", "label", "description"], entry.EnumerateObject().Select(property => property.Name));
        Assert.Equal(FirstId, entry.GetProperty("value").GetString());
        var label = $"{FirstId} ({Timestamp.ToString("O", CultureInfo.InvariantCulture)})";
        Assert.Equal(label, entry.GetProperty("label").GetString());
        Assert.Equal(label, entry.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("42")]
    [InlineData("*0924*")]
    [InlineData("nomatch")]
    [InlineData(FirstId)]
    public async Task Values_LeaveAllMatchingToTheExistingShell(string fragment)
    {
        var result = await CompleteAsync(["--completion-values", "troubleshoot", "--id", fragment],
            [new(FirstId, Timestamp), new(SecondId, Timestamp.AddMinutes(-1))]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{FirstId}\n{SecondId}{Environment.NewLine}", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Values_EmptyHistoryIsSuccessfulEmptyOutput(bool json)
    {
        var args = ValueRequest(json);
        var result = await CompleteAsync(args, []);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(json ? "[]" + Environment.NewLine : string.Empty, result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Values_DisabledCompletionDoesNotResolveHistory(bool json)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = await CompletionGenerator.TryHandleValuesAsync(
            ValueRequest(json), () => false,
            () => throw new Xunit.Sdk.XunitException("History must not be resolved."),
            output, error);
        Assert.Equal(0, result);
        Assert.Equal(json ? "[]" + Environment.NewLine : string.Empty, output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task Values_IgnoreOrdinaryRequestsWithoutReadingConfigurationOrHistory()
    {
        foreach (var args in new[] { Array.Empty<string>(), new[] { "vm", "--id", FirstId }, new[] { "--completions" } })
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var result = await CompletionGenerator.TryHandleValuesAsync(
                args, () => throw new Xunit.Sdk.XunitException("Configuration must not be read."),
                () => throw new Xunit.Sdk.XunitException("History must not be resolved."), output, error);
            Assert.Null(result);
            Assert.Empty(output.ToString());
            Assert.Empty(error.ToString());
        }
    }

    public static TheoryData<string[]> InvalidRequests => new()
    {
        new[] { "--completion-values" },
        new[] { "--completion-values", "troubleshoot" },
        new[] { "--completion-values", "troubleshoot", "show" },
        new[] { "--completion-values", "vm", "--id" },
        new[] { "--completion-values", "troubleshoot", "--list" },
        new[] { "--completion-values", "troubleshoot", "unknown", "--id" },
        new[] { "--completion-values", "troubleshoot", "--id", "42\n" },
        new[] { "--completion-values", "troubleshoot", "--id", "--json", "42" },
        new[] { "--completion-values", "troubleshoot", "--id", "--json", "--json" },
        new[] { "--completion-values", "troubleshoot", "--id", "42", "--json", "extra" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Values_InvalidRequestDoesNotResolveConfigurationOrHistory(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = await CompletionGenerator.TryHandleValuesAsync(
            args, () => throw new Xunit.Sdk.XunitException("Configuration must not be read."),
            () => throw new Xunit.Sdk.XunitException("History must not be resolved."), output, error);
        Assert.Equal(2, result);
        Assert.Empty(output.ToString());
        Assert.StartsWith("Invalid completion request.", error.ToString());
    }

    [Fact]
    public async Task Values_ValidateIndexIdsUsingTheSharedParserAndDeduplicate()
    {
        var result = await CompleteAsync(ValueRequest(false),
        [
            new(FirstId, Timestamp.AddMinutes(-1)), new(FirstId, Timestamp),
            new(SecondId, Timestamp.AddMinutes(-2)), new("00000000000000000000000000000000", Timestamp),
            new("0-2026-0924-0000-0001", Timestamp), new(FirstId + "\n", Timestamp),
            new(" " + FirstId, Timestamp), new("$(unsafe)", Timestamp), new(null!, Timestamp), null!,
        ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"{FirstId}\n{SecondId}{Environment.NewLine}", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData("failure_history_incompatible_draft", "incompatible")]
    [InlineData("failure_history_clear_pending", "cleanup")]
    [InlineData("failure_history_corrupt", "corrupt")]
    [InlineData("failure_history_key_unavailable", "key")]
    public async Task Values_DistinguishHistoryFailuresWithoutLeakingStoredDiagnostics(string code, string expected)
    {
        var result = await CompleteAsync(ValueRequest(true), [],
            new FailureHistoryException(code, "private-command --secret private-value"));
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains(expected, result.Error);
        Assert.DoesNotContain("private", result.Error);
    }

    [Fact]
    public async Task Values_ReadFreshIndexAndForwardCancellationOnEachRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        history.SetupSequence(s => s.ReadIndexAsync(cancellation.Token))
            .ReturnsAsync([new FailureHistoryEntry(FirstId, Timestamp)])
            .ReturnsAsync([new FailureHistoryEntry(SecondId, Timestamp)]);
        foreach (var expected in new[] { FirstId, SecondId })
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exit = await CompletionGenerator.TryHandleValuesAsync(
                ValueRequest(false), () => true, () => history.Object, output, error, cancellation.Token);
            Assert.Equal(0, exit);
            Assert.Equal(expected + Environment.NewLine, output.ToString());
            Assert.Empty(error.ToString());
        }
        history.Verify(s => s.ReadIndexAsync(cancellation.Token), Times.Exactly(2));
        history.VerifyNoOtherCalls();
    }

    [Fact]
    public void StaticCompletion_UsesIntrinsicHelpMetadataForRootAndShow()
    {
        var full = CompletionGenerator.CreateCompletionTree([]);
        var command = Assert.IsType<Dictionary<string, object>>(full["troubleshoot"]);
        Assert.Equal(["--id", "--list", "--style"], Assert.IsType<List<string>>(command["_params"]));
        Assert.Equal(["show"], Assert.IsType<List<string>>(command["_subcommands"]));
        Assert.Equal(["--id"], Assert.IsType<List<string>>(command["show"]));
        Assert.Equal(["--id"], Assert.IsType<List<string>>(command["show._req"]));
        Assert.Equal(["--id", "--list", "--style", "show"],
            CompletionGenerator.ComputeCandidates([], ["TROUBLESHOOT"]));
        Assert.Equal(["--id"], CompletionGenerator.ComputeCandidates([], ["troubleshoot", "show"]));
        Assert.Empty(CompletionGenerator.ComputeCandidates([], ["troubleshoot", "unknown"]));
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
        KeyValuePair<string, CommandMetadata>[] commands = [new("deployment group create", metadata)];
        var scoped = CompletionGenerator.CreateCompletionTree(commands, "deployment");
        Assert.Equal(["group create"], Assert.IsType<List<string>>(scoped["_subcommands"]));
        Assert.Equal(["--name", "--mode"], Assert.IsType<List<string>>(scoped["group create"]));
        Assert.Equal(["--name"], Assert.IsType<List<string>>(scoped["group create._req"]));
        var values = Assert.IsType<Dictionary<string, object>>(scoped["group create._values"]);
        Assert.Equal(["one", "two"], Assert.IsType<string[]>(values["--mode"]));
        Assert.False(scoped.ContainsKey("_params"));
        Assert.False(scoped.ContainsKey("troubleshoot"));
        Assert.Equal(["deployment", "troubleshoot"], CompletionGenerator.ComputeCandidates(commands, []));
        Assert.Equal(["group"], CompletionGenerator.ComputeCandidates(commands, ["deployment"]));
    }

    private static string[] ValueRequest(bool json) =>
        json ? ["--completion-values", "troubleshoot", "--id", "--json"] : ["--completion-values", "troubleshoot", "--id"];

    private static async Task<(int? ExitCode, string Output, string Error)> CompleteAsync(
        string[] args, FailureHistoryEntry[] entries, Exception? failure = null)
    {
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        var read = history.Setup(s => s.ReadIndexAsync(It.IsAny<CancellationToken>()));
        if (failure is null)
            read.ReturnsAsync(entries);
        else
            read.ThrowsAsync(failure);
        var config = new Mock<IConfigurationService>(MockBehavior.Strict);
        config.Setup(c => c.GetConfiguration<CompletionOptions>(null, null)).Returns(new CompletionOptions());
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = await CompletionGenerator.TryHandleValuesAsync(
            args, () => CompletionGenerator.ResolveEnabled(config.Object), () => history.Object, output, error);
        history.Verify(s => s.ReadIndexAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.VerifyNoOtherCalls();
        config.Verify(c => c.GetConfiguration<CompletionOptions>(null, null), Times.Once);
        config.VerifyNoOtherCalls();
        return (result, output.ToString(), error.ToString());
    }
}
