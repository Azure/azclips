// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Cli;
using Core.Cli.Recovery;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public class TroubleshootCommandTests
{
    internal const string Id = "42-2026-0924-0000-0000";

    [Theory]
    [InlineData(new[] { "troubleshoot" }, true)]
    [InlineData(new[] { "--output", "invalid", "troubleshoot", "--list" }, true)]
    [InlineData(new[] { "--query", "troubleshoot", "group", "list" }, false)]
    [InlineData(new[] { "--debug", "help", "troubleshoot", "show" }, true)]
    [InlineData(new[] { "vm", "show", "--name", "troubleshoot" }, false)]
    [InlineData(new[] { "--output=json", "TROUBLESHOOT", "--list" }, true)]
    public void EarlyRouting_RecognizesOnlyTheCommand(string[] args, bool expected) =>
        Assert.Equal(expected, GlobalArgParser.IsCommandInvocation(args, IntrinsicVerbs.Troubleshoot));

    [Fact]
    public void Parser_RecognizesAnalysisAndLocalViews()
    {
        Assert.Equal(new TroubleshootRequest(null, false), Parse("troubleshoot"));
        Assert.Equal(new TroubleshootRequest(Id, false), Parse("troubleshoot", "--id=" + Id));
        Assert.Equal(new TroubleshootRequest(Id, false, Style: "interactive"),
            Parse("troubleshoot", "--id", Id, "--style=INTERACTIVE"));
        Assert.Equal(new TroubleshootRequest(null, true),
            Parse("troubleshoot", "--list", "--output", "json", "--query", "[].id"));
        Assert.Equal(new TroubleshootRequest(Id, false, Show: true),
            Parse("TROUBLESHOOT", "SHOW", "--id", Id, "--output", "table"));
    }

    public static TheoryData<string[]> InvalidArguments => new()
    {
        new[] { "troubleshoot", "--id" },
        new[] { "troubleshoot", "--id=" },
        new[] { "troubleshoot", "--id", "..\\history" },
        new[] { "troubleshoot", "--id", "42-2026-0924-0000" },
        new[] { "troubleshoot", "--id", "abcdef0123456789abcdef0123456789" },
        new[] { "troubleshoot", "--id", Id, "--id=" + Id },
        new[] { "troubleshoot", "--unknown" },
        new[] { "troubleshoot", "--list", "--list" },
        new[] { "troubleshoot", "--list", "--id", Id },
        new[] { "troubleshoot", "--list", "show", "--id", Id },
        new[] { "troubleshoot", "show", "--list", "--id", Id },
        new[] { "troubleshoot", "show" },
        new[] { "troubleshoot", "show", "--style", "oneshot", "--id", Id },
        new[] { "troubleshoot", "show", "show", "--id", Id },
        new[] { "troubleshoot", "--list", "--style=interactive" },
        new[] { "troubleshoot", "--style" },
        new[] { "troubleshoot", "--style=" },
        new[] { "troubleshoot", "--style", "unknown" },
        new[] { "troubleshoot", "--style=oneshot", "--style", "interactive" },
        new[] { "troubleshoot", "--id", "--style", "oneshot" },
        new[] { "troubleshoot", "--output", "json" },
        new[] { "troubleshoot", "--output", "yaml" },
        new[] { "troubleshoot", "--query", "@" },
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidArguments_FailBeforeHistoryAuthOrAgent(string[] args)
    {
        Assert.Equal(2, Assert.Throws<UsageErrorException>(() => Parse(args)).ExitCode);
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        var (exit, output, error) = await RunLocalAsync(args, LocalConfiguration(), history);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.NotEmpty(error);
        history.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("manual", "oneshot")]
    [InlineData("manual", "interactive")]
    [InlineData("auto", "oneshot")]
    [InlineData("auto", "interactive")]
    public void Analysis_UsesTheNewTriggerAndSharedStyle(string trigger, string style)
    {
        var options = new AgentOptions { Recovery = new() { Trigger = trigger, Style = style } };
        TroubleshootCommand.RequireAnalysis(options, null, false, false, false);
        TroubleshootCommand.RequireAnalysis(options, "oneshot", false, true, true);
        Assert.Equal(style, options.Recovery.Style);
    }

    [Fact]
    public void Analysis_RejectsOffRecursionAndInteractiveRedirection()
    {
        var off = Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootCommand.RequireAnalysis(new() { Recovery = new() { Trigger = "off" } }, null, false, false, false));
        Assert.Equal("AgentDisabled", off.Code);
        Assert.Contains("agent.recovery --value manual", off.Message);
        Assert.DoesNotContain("agent.enabled", off.Message);
        Assert.Equal("RecursiveRecovery", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootCommand.RequireAnalysis(Manual(), null, true, false, false)).Code);
        Assert.Throws<UsageErrorException>(() =>
            TroubleshootCommand.RequireAnalysis(Manual(), "interactive", false, true, false));
        Assert.Throws<UsageErrorException>(() =>
            TroubleshootCommand.RequireAnalysis(Manual(), "interactive", false, false, true));
    }

    [Fact]
    public async Task List_IsReadOnlyAndUsesOnlyTheThreeOriginalFields()
    {
        var record = MakeRecord() with { ErrorText = "original\r\nfailure é \"quoted\"" + new string('x', 220) };
        var config = LocalConfiguration();
        var history = ReadOnlyHistory(record);
        var (exit, output, error) = await RunLocalAsync(["troubleshoot", "--list", "--output", "json"], config, history);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        var entry = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(["id", "command", "errorDetail"], entry.EnumerateObject().Select(property => property.Name));
        Assert.Equal(record.FailedCommand, entry.GetProperty("command").GetString());
        Assert.Equal(TroubleshootOutput.SummarizeError(record.ErrorText), entry.GetProperty("errorDetail").GetString());
        Assert.Contains("é", output);
        config.Verify(c => c.GetConfiguration<AgentOptions>(null, null), Times.Never);
        history.Verify(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Show_UsesFullOriginalContentAndSupportsQuery()
    {
        var record = MakeRecord() with { ErrorText = "first\nsecond\r\n" + new string('é', 400) };
        foreach (var query in new[] { Array.Empty<string>(), new[] { "--query", "errorDetail" } })
        {
            var history = ReadOnlyHistory(record);
            var (exit, output, error) = await RunLocalAsync(
                ["troubleshoot", "show", "--id", record.Id, "--output", "json", .. query],
                LocalConfiguration(), history);
            Assert.Equal(0, exit);
            Assert.Empty(error);
            using var json = JsonDocument.Parse(output);
            Assert.Equal(record.ErrorText, query.Length == 0
                ? json.RootElement.GetProperty("errorDetail").GetString()
                : json.RootElement.GetString());
            history.Verify(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()), Times.Once);
            history.VerifyNoOtherCalls();
        }
    }

    [Fact]
    public async Task LocalViews_DistinguishEmptyAndMissingRecords()
    {
        var (exit, output, error) = await RunLocalAsync(
            ["troubleshoot", "--list", "--output", "json"], LocalConfiguration(), ReadOnlyHistory());
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("[]", output.Trim());

        var missing = await RunLocalAsync(
            ["troubleshoot", "show", "--id", Id, "--output", "json"], LocalConfiguration(), ReadOnlyHistory());
        Assert.Equal(1, missing.Exit);
        Assert.Empty(missing.Output);
        Assert.Contains("FailureRecordNotFound", missing.Error);
    }

    [Theory]
    [InlineData("failure_history_incompatible_draft")]
    [InlineData("failure_history_key_unavailable")]
    [InlineData("failure_history_corrupt")]
    [InlineData("failure_history_clear_pending")]
    public async Task LocalViews_ReportHistoryErrorsWithoutCleanupOrFallback(string code)
    {
        foreach (var view in new[] { new[] { "--list" }, new[] { "show", "--id", Id } })
        {
            var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
            history.Setup(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FailureHistoryException(code, "Local history cannot be read."));
            var result = await RunLocalAsync(
                ["troubleshoot", .. view, "--output", "json"], LocalConfiguration(), history);
            Assert.Equal(1, result.Exit);
            Assert.Empty(result.Output);
            Assert.Contains($"[{code}]", result.Error);
            history.Verify(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()), Times.Once);
            history.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 130)]
    public async Task LocalRead_OnlyCallerCancellationReturns130(bool cancelled, int expectedExit)
    {
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        history.Setup(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The read timed out."));
        var result = await RunLocalAsync(
            ["troubleshoot", "--list", "--output", "json"], LocalConfiguration(), history, new CancellationToken(cancelled));
        Assert.Equal(expectedExit, result.Exit);
        Assert.Empty(result.Output);
        if (cancelled)
            Assert.Empty(result.Error);
        else
            Assert.Contains("TroubleshootFailed", result.Error);
        history.Verify(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DisabledAnalysis_DoesNotReadHistoryOrStartAuth()
    {
        var config = LocalConfiguration();
        config.Setup(c => c.GetConfiguration<AgentOptions>(null, null))
            .Returns(new AgentOptions { Recovery = new() { Trigger = "off" } });
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        var result = await RunLocalAsync(["troubleshoot"], config, history);
        Assert.Equal(1, result.Exit);
        Assert.Empty(result.Output);
        Assert.Contains("agent.recovery", result.Error);
        history.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(new[] { "troubleshoot" }, "NoFailureHistory")]
    [InlineData(new[] { "troubleshoot", "--id", Id }, "FailureRecordNotFound")]
    public async Task Analysis_EmptyOrEvictedSelectionCannotStartAgent(string[] args, string expectedCode)
    {
        var config = LocalConfiguration();
        config.Setup(c => c.GetConfiguration<AgentOptions>(null, null)).Returns(Manual());
        var history = ReadOnlyHistory();
        var result = await RunLocalAsync(args, config, history);
        Assert.Equal(1, result.Exit);
        Assert.Empty(result.Output);
        Assert.Contains(expectedCode, result.Error);
        history.Verify(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(new[] { "--debug", "help", "troubleshoot" }, "az troubleshoot", "--style")]
    [InlineData(new[] { "troubleshoot", "show", "--help" }, "az troubleshoot show", "--id")]
    public async Task Help_RemainsLocalWithExamplesLast(string[] args, string command, string parameter)
    {
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        var (exit, output, error) = await RunLocalAsync(args, LocalConfiguration(), history);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains(command, output);
        Assert.Contains(parameter, output);
        Assert.DoesNotContain("----id", output);
        Assert.True(output.IndexOf("# EXAMPLES", StringComparison.Ordinal) >
            output.IndexOf("# GLOBAL ARGUMENTS", StringComparison.Ordinal));
        history.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("oneshot", null, "oneshot")]
    [InlineData("interactive", null, "interactive")]
    [InlineData("oneshot", "interactive", "interactive")]
    [InlineData("interactive", "oneshot", "oneshot")]
    public async Task ManualRecovery_UsesOriginalInputEffectiveStyleAndIndependentTelemetry(
        string configured, string? styleOverride, string effective)
    {
        var failure = MakeRecord() with
        {
            ErrorText = "original\n\"error\" 雪 " + new string('e', 80_000),
            CommandTraceId = ActivityTraceId.CreateRandom().ToHexString(),
            CommandSpanId = ActivitySpanId.CreateRandom().ToHexString(),
        };
        using var source = new ActivitySource("troubleshoot-" + Guid.NewGuid().ToString("N"));
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        var telemetry = CreateTelemetry(source);
        var options = new AgentOptions { Provider = "github", Recovery = new() { Style = configured } };

        var exit = await TroubleshootCommand.RunAttemptAsync(
            failure, options, telemetry.Object, ("test-tenant", "EU"), (request, traceparent, _) =>
            {
                Assert.Single(stopped);
                Assert.Equal(failure.FailedCommand, request.FailedCommand);
                Assert.Equal(failure.ErrorText, request.ErrorText);
                Assert.Equal(failure.Id, request.FailureId);
                Assert.Equal(failure.CommandTraceId, request.CommandTraceId);
                Assert.Equal(effective, request.SessionStyle);
                Assert.Equal(RecoveryTrigger.Manual, request.Trigger);
                Assert.Equal(stopped[0].TraceId.ToHexString(), request.RequestTraceId);
                Assert.Equal(stopped[0].SpanId.ToHexString(), request.RequestSpanId);
                Assert.Equal("test-tenant", request.TenantId);
                Assert.Equal("EU", request.DataBoundary);
                var activity = Assert.IsType<Activity>(Activity.Current);
                Assert.Equal(default, activity.ParentSpanId);
                Assert.NotEqual(failure.CommandTraceId, activity.TraceId.ToHexString());
                Assert.NotEqual(request.RequestTraceId, activity.TraceId.ToHexString());
                Assert.Equal(activity.Id, traceparent);
                Assert.Equal(effective, activity.GetTagItem(TelemetryTags.RecoverySessionStyle));
                Assert.Equal("manual", activity.GetTagItem(TelemetryTags.RecoveryTrigger));
                Assert.Equal("github", activity.GetTagItem(TelemetryTags.RecoveryProvider));
                return Task.FromResult(17);
            }, CancellationToken.None, styleOverride);

        Assert.Equal(17, exit);
        Assert.Equal(configured, options.Recovery.Style);
        Assert.Equal(ActivityStatusCode.Error, stopped[1].Status);
        Assert.Equal(17, stopped[1].GetTagItem(TelemetryTags.RecoveryExitCode));
    }

    [Fact]
    public async Task ManualRecovery_OptOutDoesNotInventTraceIdsOrReplaceFailures()
    {
        var telemetry = CreateTelemetry();
        var failure = MakeRecord() with { CommandTraceId = "private-command", CommandSpanId = "private-error" };
        using var cancellation = new CancellationTokenSource();
        var exit = await TroubleshootCommand.RunAttemptAsync(
            failure, new(), telemetry.Object, (null, null), (request, traceparent, token) =>
            {
                Assert.Null(traceparent);
                Assert.Null(request.CommandTraceId);
                Assert.Null(request.CommandSpanId);
                Assert.Null(request.RequestTraceId);
                Assert.Null(request.RequestSpanId);
                Assert.Null(request.TenantId);
                Assert.Null(request.DataBoundary);
                Assert.Equal(cancellation.Token, token);
                return Task.FromResult(130);
            }, cancellation.Token);
        Assert.Equal(130, exit);
    }

    [Theory]
    [InlineData(false, ActivityStatusCode.Error, 1)]
    [InlineData(true, ActivityStatusCode.Unset, 130)]
    public async Task ManualRecovery_TimeoutsAreFailuresUnlessTheCallerCancelled(
        bool cancelled, ActivityStatusCode expectedStatus, int expectedExit)
    {
        using var source = new ActivitySource("troubleshoot-cancellation-" + Guid.NewGuid().ToString("N"));
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var telemetry = CreateTelemetry(source);
        await Assert.ThrowsAsync<TaskCanceledException>(() => TroubleshootCommand.RunAttemptAsync(
            MakeRecord(), new(), telemetry.Object, (null, null),
            (_, _, _) => Task.FromException<int>(new TaskCanceledException("HTTP request timed out.")),
            new CancellationToken(cancelled)));
        Assert.Equal(expectedStatus, stopped[1].Status);
        Assert.Equal(expectedExit, stopped[1].GetTagItem(TelemetryTags.RecoveryExitCode));
    }

    [Theory]
    [InlineData("oneshot", 0, 0)]
    [InlineData("oneshot", 1, 1)]
    [InlineData("interactive", 0, 0)]
    [InlineData("interactive", 7, 7)]
    [InlineData("oneshot", 130, 130)]
    [InlineData("interactive", 130, 130)]
    public async Task Manual_ReturnsTheAgentExitCodeForBothStyles(string style, int agentExit, int expected)
    {
        var options = new AgentOptions { Recovery = new RecoveryOptions { Trigger = "manual", Style = style } };
        var exit = await TroubleshootCommand.RunAttemptAsync(
            MakeRecord(), options, CreateTelemetry().Object, (null, null),
            (_, _, _) => Task.FromResult(agentExit), CancellationToken.None);
        Assert.Equal(expected, exit);
    }

    [Theory]
    [InlineData(false, 2, 0, 2)]
    [InlineData(false, 2, 1, 2)]
    [InlineData(false, 1, 0, 1)]
    [InlineData(true, 2, 0, 0)]
    [InlineData(true, 2, 5, 5)]
    [InlineData(false, 2, 130, 130)]
    [InlineData(true, 2, 130, 130)]
    public void Automatic_OneshotKeepsTheOriginalCodeAndInteractiveReturnsTheAgentCode(
        bool interactive, int original, int agentExit, int expected) =>
        Assert.Equal(expected, Program.AutomaticRecoveryExitCode(interactive, original, agentExit));

    private static Mock<ITelemetryService> CreateTelemetry(ActivitySource? source = null)
    {
        var telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
        telemetry.Setup(service => service.StartRootActivity(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()))
            .Returns((DateTimeOffset _, string? name) => source?.StartActivity(name ?? "CLI.Activity.Root", ActivityKind.Server));
        return telemetry;
    }

    private static TroubleshootRequest Parse(params string[] args) =>
        TroubleshootRequest.Parse(GlobalArgParser.Parse(args));

    private static AgentOptions Manual() => new() { Recovery = new() { Trigger = "manual" } };

    private static Mock<IConfigurationService> LocalConfiguration()
    {
        var config = new Mock<IConfigurationService>(MockBehavior.Strict);
        config.Setup(c => c.GetConfiguration<CoreOptions>(null, null)).Returns(new CoreOptions());
        config.Setup(c => c.GetConfiguration<ClientsOptions>(null, null)).Returns(new ClientsOptions());
        return config;
    }

    private static Mock<IFailureHistoryStore> ReadOnlyHistory(params FailureRecord[] records)
    {
        var history = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        history.Setup(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(records);
        return history;
    }

    private static async Task<(int Exit, string Output, string Error)> RunLocalAsync(
        string[] args, Mock<IConfigurationService> config, Mock<IFailureHistoryStore> history,
        CancellationToken cancellationToken = default)
    {
        var home = Path.Combine(Directory.GetCurrentDirectory(), ".test-artifacts", "troubleshoot", Guid.NewGuid().ToString("N"));
        var previousHome = Environment.GetEnvironmentVariable("AZCLIPS_HOME");
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        var telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
        var oldOutput = Console.Out;
        var oldError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Environment.SetEnvironmentVariable("AZCLIPS_HOME", home);
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(config.Object);
            services.AddSingleton(history.Object);
            services.AddSingleton(auth.Object);
            services.AddSingleton(profiles.Object);
            services.AddSingleton(telemetry.Object);
        });
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
            var exit = await TroubleshootCommand.RunAsync(args, cancellationToken);
            auth.VerifyNoOtherCalls();
            profiles.VerifyNoOtherCalls();
            telemetry.VerifyNoOtherCalls();
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(oldOutput);
            Console.SetError(oldError);
            AzclipsServiceProvider.Dispose();
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", previousHome);
            if (Directory.Exists(home))
                Directory.Delete(home, recursive: true);
        }
    }

    internal static FailureRecord MakeRecord(uint pid = 42, ulong sequence = 0) => new()
    {
        Id = FailureId.Format(pid, new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero), sequence),
        TerminalSessionId = pid,
        Sequence = sequence,
        HistoryGeneration = 1,
        OccurredAtUtc = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero),
        Arguments = ["group", "show", "--name", "test-only-secret"],
        FailedCommand = "group show --name \"test-only-secret\"",
        ErrorText = "original failure with test-only-secret",
        CommandName = "group show",
        ErrorCode = "TestFailure",
        ExitCode = 1,
    };
}
