// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Cli.Recovery;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public class TroubleshootCommandTests
{
    [Theory]
    [InlineData(new[] { "troubleshoot" }, true)]
    [InlineData(new[] { "--output", "invalid", "troubleshoot", "--list" }, true)]
    [InlineData(new[] { "--query", "troubleshoot", "group", "list" }, false)]
    [InlineData(new[] { "--debug", "help", "troubleshoot" }, true)]
    [InlineData(new[] { "vm", "show", "--name", "troubleshoot" }, false)]
    [InlineData(new[] { "--output=json", "TROUBLESHOOT", "--list" }, true)]
    public void EarlyRouting_RecognizesOnlyTheCommand(string[] args, bool expected)
    {
        Assert.Equal(expected, GlobalArgParser.IsCommandInvocation(args, IntrinsicVerbs.Troubleshoot));
    }

    [Fact]
    public void Parser_DefaultsToLatestAndSupportsExactIdAndList()
    {
        var id = Guid.NewGuid().ToString("N");
        Assert.Equal(new TroubleshootRequest(null, false), TroubleshootRequest.Parse(GlobalArgParser.Parse(["troubleshoot"])));
        Assert.Equal(new TroubleshootRequest(id, false), TroubleshootRequest.Parse(GlobalArgParser.Parse(["troubleshoot", "--id=" + id])));
        Assert.Equal(new TroubleshootRequest(null, true), TroubleshootRequest.Parse(GlobalArgParser.Parse(["troubleshoot", "--list", "--output", "json"])));
    }

    public static TheoryData<string[]> InvalidArguments => new()
    {
        new[] { "troubleshoot", "--id" },
        new[] { "troubleshoot", "--id", "..\\history" },
        new[] { "troubleshoot", "--unknown" },
        new[] { "troubleshoot", "--list", "--list" },
        new[] { "troubleshoot", "--output", "json" },
        new[] { "troubleshoot", "--query", "@" },
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Parser_RejectsInvalidOrUnsupportedArguments(string[] args)
    {
        Assert.Equal(2, Assert.Throws<UsageErrorException>(() =>
            TroubleshootRequest.Parse(GlobalArgParser.Parse(args))).ExitCode);
    }

    [Fact]
    public void Parser_RejectsMixedSelection()
    {
        Assert.Throws<UsageErrorException>(() => TroubleshootRequest.Parse(
            GlobalArgParser.Parse(["troubleshoot", "--list", "--id", Guid.NewGuid().ToString("N")])));
    }

    [Fact]
    public void Selection_NeverSubstitutesLatestForAnUnknownId()
    {
        var first = MakeRecord();
        var second = MakeRecord();
        Assert.Same(first, TroubleshootCommand.SelectRecord([first, second], null));
        Assert.Same(second, TroubleshootCommand.SelectRecord([first, second], second.Id.ToUpperInvariant()));
        Assert.Equal("FailureRecordNotFound", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootCommand.SelectRecord([first], Guid.NewGuid().ToString("N"))).Code);
        Assert.Equal("NoFailureHistory", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootCommand.SelectRecord([], null)).Code);
    }

    [Theory]
    [InlineData(false, "oneshot")]
    [InlineData(true, "oneshot")]
    [InlineData(false, "interactive")]
    [InlineData(true, "interactive")]
    public void ExplicitTrigger_OnlyRequiresMasterSwitch(bool autoEnabled, string style)
    {
        TroubleshootCommand.RequireAgentEnabled(new()
        {
            Enabled = true,
            AutoRecovery = new() { Enabled = autoEnabled },
            SessionStyle = style,
        }, inAgentMode: false);
    }

    [Fact]
    public void ExplicitTrigger_RejectsDisabledAndRecursiveCalls()
    {
        Assert.Equal("AgentDisabled", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootCommand.RequireAgentEnabled(new(), false)).Code);
        Assert.Equal("RecursiveRecovery", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootCommand.RequireAgentEnabled(new() { Enabled = true }, true)).Code);
    }

    [Theory]
    [InlineData(false, false, "oneshot", false, false, false)]
    [InlineData(false, true, "oneshot", false, false, false)]
    [InlineData(true, false, "oneshot", false, false, false)]
    [InlineData(true, true, "oneshot", true, true, true)]
    [InlineData(true, true, "interactive", false, false, true)]
    [InlineData(true, true, "interactive", true, false, false)]
    [InlineData(true, true, "interactive", false, true, false)]
    public void AutomaticTrigger_HonorsBothSwitchesAndTheExistingTtyGuard(
        bool enabled, bool automatic, string style, bool inputRedirected, bool outputRedirected, bool expected)
    {
        var options = new AgentOptions
        {
            Enabled = enabled,
            AutoRecovery = new() { Enabled = automatic },
            SessionStyle = style,
        };
        Assert.Equal(expected, AutomaticRecoveryPolicy.ShouldStart(options, false, inputRedirected, outputRedirected));
        Assert.False(AutomaticRecoveryPolicy.ShouldStart(options, true, inputRedirected, outputRedirected));
    }

    [Fact]
    public async Task List_ReadsLocalDataWithMasterOffAndNeverResolvesAuthOrTelemetry()
    {
        var record = MakeRecord();
        var config = LocalConfiguration();
        var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        store.Setup(s => s.ReadRecordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { record });
        var (exit, output, error) = await RunLocalAsync(["troubleshoot", "--list", "--output", "json"], config, store);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(record.Id, json.RootElement[0].GetProperty("id").GetString());
        Assert.DoesNotContain(record.ErrorText, output);
        Assert.DoesNotContain("test-only-secret", output);
        config.Verify(c => c.GetConfiguration<AgentOptions>(null, null), Times.Never);
        config.Verify(c => c.GetConfiguration<TelemetryOptions>(null, null), Times.Never);
        store.Verify(s => s.SaveAsync(It.IsAny<FailureRecord>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisabledAnalysis_DoesNotReadHistoryOrStartAuth()
    {
        var config = LocalConfiguration();
        config.Setup(c => c.GetConfiguration<AgentOptions>(null, null)).Returns(new AgentOptions());
        var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);

        var (exit, output, error) = await RunLocalAsync(["troubleshoot"], config, store);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Contains("AgentDisabled", error);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Help_RemainsLocalAndDoesNotRequireAgentOrHistory()
    {
        var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        var (exit, output, error) = await RunLocalAsync(["--debug", "help", "troubleshoot"], LocalConfiguration(), store);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains("--id", output);
        Assert.DoesNotContain("----id", output);
        Assert.Contains("--list", output);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Capture_PreservesFullInputAndOnlyHintsWithAutomaticOff()
    {
        foreach (var automatic in new[] { false, true })
        {
            FailureRecord? saved = null;
            var args = new[] { "group", "create", "--name", "test-only-secret", "--output", "json" };
            var error = new HttpRequestException("original\n" + new string('e', 80_000), null, HttpStatusCode.BadRequest);
            var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
            store.Setup(s => s.SaveAsync(It.IsAny<FailureRecord>(), 10, It.IsAny<CancellationToken>()))
                .Callback<FailureRecord, int, CancellationToken>((record, _, _) => saved = record)
                .ReturnsAsync((FailureRecord record, int _, CancellationToken _) => new FailureHistoryEntry(record.Id, record.OccurredAtUtc));

            var result = await FailureCapture.SaveAsync(
                args, "group create --name test-only-secret", error, 1, "group create", null,
                () => new() { Enabled = true, AutoRecovery = new() { Enabled = automatic } },
                () => store.Object, false);

            Assert.NotNull(saved);
            Assert.Equal(args, saved.Arguments);
            Assert.Equal(error.Message, saved.ErrorText);
            Assert.Equal(400, saved.HttpStatusCode);
            Assert.Equal(!automatic, result.ShowHint);
            Assert.NotNull(result.Entry);
        }
    }

    [Fact]
    public async Task Capture_ExcludesDisabledSuccessfulCancelledAndUtilityInvocations()
    {
        var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        foreach (var args in new[]
        {
            new[] { "troubleshoot" },
            new[] { "--output", "bad", "troubleshoot", "--list" },
            new[] { "--completion-values", "troubleshoot", "--id" },
            new[] { "--upload-telemetry" },
        })
        {
            var result = await FailureCapture.SaveAsync(args, "unused", new Exception("unused"), 1, null, null,
                () => new() { Enabled = true }, () => store.Object, false);
            Assert.Null(result.Entry);
        }
        Assert.Null((await FailureCapture.SaveAsync(["group", "list"], "group list", new Exception("unused"), 1, null, null,
            () => new(), () => store.Object, false)).Entry);
        Assert.Null((await FailureCapture.SaveAsync(["group", "list"], "group list", new Exception("unused"), 0, null, null,
            () => new() { Enabled = true }, () => store.Object, false)).Entry);
        Assert.Null((await FailureCapture.SaveAsync(["group", "list"], "group list", new OperationCanceledException(), 1, null, null,
            () => new() { Enabled = true }, () => store.Object, false, new CancellationToken(true))).Entry);
        Assert.Null((await FailureCapture.SaveAsync(["group", "list"], "group list", new Exception("unused"), 1, null, null,
            () => new() { Enabled = true }, () => store.Object, true)).Entry);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Capture_ReportsStorageFailureWithoutInventingAnId()
    {
        var store = new Mock<IFailureHistoryStore>();
        store.Setup(s => s.SaveAsync(It.IsAny<FailureRecord>(), 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FailureHistoryException("KeyUnavailable", "Protected key storage is unavailable."));

        var result = await FailureCapture.SaveAsync(["group", "list"], "group list", new Exception("original failure"), 1, null, null,
            () => new() { Enabled = true }, () => store.Object, false);

        Assert.Null(result.Entry);
        Assert.False(result.ShowHint);
        Assert.Contains("KeyUnavailable", result.Warning);
    }

    [Fact]
    public void StructuredError_ContainsRecoveryMetadataWithoutExtraText()
    {
        var oldError = Console.Error;
        using var output = new StringWriter();
        var entry = new FailureHistoryEntry(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        var capture = new FailureCaptureResult(entry, true);
        Console.SetError(output);
        try
        {
            Core.Cli.Program.EmitStructuredError(AzclipsErrorException.UsageError("original"), OutputFormat.Json, false, null, capture);
            Core.Cli.Program.EmitRecoveryDiagnostic(capture, OutputFormat.Json, false, structuredErrorAlreadyWritten: true);
        }
        finally
        {
            Console.SetError(oldError);
        }
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("original", json.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(entry.Id, json.RootElement.GetProperty("recovery").GetProperty("id").GetString());
    }

    [Fact]
    public void ManualSpawn_OnlyPassesAPipeHandleAndPreservesInheritedStdio()
    {
        var start = AgentProcessRunner.BuildManualStartInfo(
            Path.Combine(AppContext.BaseDirectory, "azclips-agent.exe"), "123", null);

        Assert.Equal(new[] { "--agent-mode", RecoveryRequest.InputPipeArgument, "123" }, start.ArgumentList);
        Assert.False(start.RedirectStandardInput);
        Assert.False(start.RedirectStandardOutput);
        Assert.False(start.RedirectStandardError);
        Assert.Equal("1", start.Environment["AZCLIPS_IN_AGENT_MODE"]);
        Assert.False(start.Environment.ContainsKey(Core.Cli.Program.RecoveryTraceparentEnvVar));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("raw-command-or-secret")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("123456789012345678901234567890123!")]
    public void HistoricalTelemetryIdentifiers_DoNotExportArbitraryText(string? value)
    {
        Assert.Null(TroubleshootCommand.ValidTraceIdentifier(value, 32));
    }

    [Fact]
    public async Task ManualTelemetry_SeparatesOriginalRequestAndRecoveryAndHandsOffRouting()
    {
        var failure = MakeRecord() with
        {
            CommandTraceId = ActivityTraceId.CreateRandom().ToHexString(),
            CommandSpanId = ActivitySpanId.CreateRandom().ToHexString(),
        };
        var tenantId = Guid.NewGuid().ToString();
        using var source = new ActivitySource("manual-recovery-test-" + Guid.NewGuid().ToString("N"));
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        var telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
        telemetry.Setup(service => service.StartRootActivity(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()))
            .Returns((DateTimeOffset _, string? name) => source.StartActivity(name ?? "CLI.Activity.Root", ActivityKind.Server));
        var options = new AgentOptions { Enabled = true, SessionStyle = "interactive" };

        var exit = await TroubleshootCommand.RunAttemptAsync(
            failure, options, telemetry.Object, (tenantId, "EU"),
            (request, traceparent, _) =>
            {
                Assert.Single(stopped);
                Assert.Equal("CLI.Activity.Root", stopped[0].OperationName);
                Assert.Same(failure, request.Failure);
                Assert.Equal(stopped[0].TraceId.ToHexString(), request.RequestTraceId);
                Assert.Equal(stopped[0].SpanId.ToHexString(), request.RequestSpanId);
                Assert.Equal(tenantId, request.TenantId);
                Assert.Equal("EU", request.DataBoundary);
                var recovery = Assert.IsType<Activity>(Activity.Current);
                Assert.Equal("CLI.Activity.AiRecovery", recovery.OperationName);
                Assert.Equal(default, recovery.ParentSpanId);
                Assert.NotEqual(failure.CommandTraceId, recovery.TraceId.ToHexString());
                Assert.NotEqual(request.RequestTraceId, recovery.TraceId.ToHexString());
                Assert.Equal(recovery.Id, traceparent);
                Assert.Equal(failure.CommandTraceId, recovery.GetTagItem(TelemetryTags.CommandTraceId));
                Assert.Equal("oneshot", recovery.GetTagItem(TelemetryTags.RecoverySessionStyle));
                Assert.Equal("manual", recovery.GetTagItem(TelemetryTags.RecoveryTrigger));
                Assert.Equal(tenantId, recovery.GetTagItem(TelemetryTags.TenantId));
                Assert.Equal("EU", recovery.GetTagItem(TelemetryTags.DataBoundary));
                return Task.FromResult(0);
            }, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal("interactive", options.SessionStyle);
        Assert.Equal(2, stopped.Count);
        Assert.Equal(ActivityStatusCode.Ok, stopped[1].Status);
    }

    [Fact]
    public async Task ManualTelemetry_OptOutDoesNotPreventDiagnosisOrInventTraceIds()
    {
        var telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
        telemetry.Setup(service => service.StartRootActivity(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()))
            .Returns((Activity?)null);

        var exit = await TroubleshootCommand.RunAttemptAsync(
            MakeRecord(), new() { Enabled = true }, telemetry.Object, (null, null),
            (request, traceparent, _) =>
            {
                Assert.Null(traceparent);
                Assert.Null(request.RequestTraceId);
                Assert.Null(request.RequestSpanId);
                Assert.Null(request.TenantId);
                Assert.Null(request.DataBoundary);
                return Task.FromResult(17);
            }, CancellationToken.None);

        Assert.Equal(17, exit);
    }

    private static Mock<IConfigurationService> LocalConfiguration()
    {
        var config = new Mock<IConfigurationService>(MockBehavior.Strict);
        config.Setup(c => c.GetConfiguration<CoreOptions>(null, null)).Returns(new CoreOptions());
        config.Setup(c => c.GetConfiguration<ClientsOptions>(null, null)).Returns(new ClientsOptions());
        return config;
    }

    private static async Task<(int Exit, string Output, string Error)> RunLocalAsync(
        string[] args, Mock<IConfigurationService> config, Mock<IFailureHistoryStore> store)
    {
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        var telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
        var oldOutput = Console.Out;
        var oldError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(config.Object);
            services.AddSingleton(store.Object);
            services.AddSingleton(auth.Object);
            services.AddSingleton(profiles.Object);
            services.AddSingleton(telemetry.Object);
        });
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exit = await Core.Cli.Program.Main(args);
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
        }
    }

    private static FailureRecord MakeRecord() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        OccurredAtUtc = DateTimeOffset.UtcNow,
        Arguments = ["group", "show", "--name", "test-only-secret"],
        FailedCommand = "group show --name test-only-secret",
        ErrorText = "original failure with test-only-secret",
        CommandName = "group show",
        ErrorCode = "TestFailure",
        ExitCode = 1,
    };
}
