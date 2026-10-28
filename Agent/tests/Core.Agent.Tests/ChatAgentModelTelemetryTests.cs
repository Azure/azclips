// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Telemetry;
using Core.Telemetry;
using GitHub.Copilot;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public class ChatAgentModelTelemetryTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    // Longer than the five-second query budget, shorter than TestTimeout, so a breached budget
    // still finishes inside the test's own ceiling.
    private static readonly TimeSpan SlowModelReply = TimeSpan.FromSeconds(8);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("user")]
    public async Task Github_RecordsSdkModelBeforeSendStops_WithoutSelectingAModel(string? initiator)
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("github-model", initiator: initiator), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);
        using var parent = new Activity("test-parent").Start();

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        var stopped = Assert.Single(capture.Stopped);
        Assert.Equal("github-model", stopped.Model);
        Assert.Equal(default, stopped.Span.ParentSpanId);
        Assert.NotEqual(parent.TraceId, stopped.Span.TraceId);
        Assert.Null(parent.GetTagItem(TelemetryTags.AgentModel));
        Assert.False(runtime.CreateOptions.TryGetProperty("model", out _));
    }

    [Fact]
    public async Task Builtin_PreservesConfiguredModel()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("wire-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime, "custom-builtin-deployment");

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("custom-builtin-deployment", Assert.Single(capture.Stopped).Model);
        Assert.Equal("custom-builtin-deployment", runtime.CreateOptions.GetProperty("model").GetString());
        Assert.Equal(0, runtime.ModelQueryCount);
    }

    [Theory]
    [InlineData("child-agent", null)]
    [InlineData("child-agent", "user")]
    [InlineData(null, "sub-agent")]
    [InlineData(null, "mcp-sampling")]
    public async Task Github_IgnoresSubagentAndAuxiliaryModels(string? agentId, string? initiator)
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [Usage("main-model"), Usage("auxiliary-model", agentId, initiator), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("main-model", Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task Github_RecordsLastMainAgentModelWithinSend()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [Usage("first-model"), Usage("fallback-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("fallback-model", Assert.Single(capture.Stopped).Model);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // The bound sits at the ingestion site in ChatAgent, not inside RecordModel, so these last two
    // are the only cases that can fail on that line. Reverting it to a null-or-whitespace check
    // leaves the empty cases green -- they are rejected either way -- and lets an over-long or
    // space-bearing name straight through to the exporter.
    [InlineData("model with spaces")]
    [InlineData("overlong")]
    public async Task Github_UnusableUsageDoesNotEraseObservedModel(string unusableModel)
    {
        using var capture = new SendCapture();
        var model = unusableModel == "overlong" ? new string('m', 129) : unusableModel;
        await using var runtime = new CopilotRuntimeStub(
            [Usage("main-model"), Usage(model), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("main-model", Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task Github_ModelObservationDoesNotLeakAcrossSends()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [Usage("first-model"), Idle()],
            [Usage(""), Usage("child-model", agentId: "child"), Idle()],
            [Usage("next-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        for (var i = 0; i < 3; i++)
            await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(new string?[] { "first-model", null, "next-model" },
            capture.Stopped.Select(send => send.Model));
    }

    [Fact]
    public async Task Github_NoUsageLeavesModelAbsent()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Null(Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task SessionErrorAfterSendCompletes_DoesNotBecomeLastError()
    {
        await using var runtime = new CopilotRuntimeStub([Idle()]);
        await using var rig = await CreateAgentAsync(runtime);
        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        rig.Agent.OnSessionErrored(new SessionErrored("rate_limit", "late", ErrorCode: null, StatusCode: 429,
            ProviderCallId: null, At: DateTimeOffset.UtcNow));

        Assert.Null(rig.Agent.LastError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Github_CancellationPreservesOnlyObservedModel_AndIgnoresLateUsage(bool reportUsage)
    {
        using var capture = new SendCapture();
        var message = new AssistantMessageEvent
        {
            Data = new AssistantMessageData { MessageId = "cancel-boundary", Content = "cancel now" },
        };
        SessionEvent[] cancelledTurn = reportUsage ? [Usage("observed-model"), message] : [message];
        await using var runtime = new CopilotRuntimeStub(cancelledTurn, [Idle()]);
        await using var rig = await CreateAgentAsync(runtime);
        using var cts = new CancellationTokenSource();
        using var cancelOnMessage = rig.Session.On<AssistantMessageEvent>(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => rig.Agent.SendAsync("test prompt", cts.Token).WaitAsync(TestTimeout));

        var stopped = Assert.Single(capture.Stopped);
        Assert.Equal(reportUsage ? "observed-model" : null, stopped.Model);
        var lateUsageProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lateUsage = rig.Session.On<AssistantUsageEvent>(_ => lateUsageProcessed.TrySetResult());
        await runtime.EmitAsync(Usage("late-model"));
        await lateUsageProcessed.Task.WaitAsync(TestTimeout);
        Assert.Equal(stopped.Model, stopped.Span.GetTagItem(TelemetryTags.AgentModel));

        await rig.Agent.SendAsync("next prompt", CancellationToken.None).WaitAsync(TestTimeout);
        Assert.Null(capture.Stopped[1].Model);
    }

    [Fact]
    public async Task Github_ErrorPreservesObservedModel_AndDoesNotChangeFailure()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [Usage("observed-model"), new SessionErrorEvent
            {
                Data = new SessionErrorData { ErrorType = "test-error", Message = "test failure" },
            }]);
        await using var rig = await CreateAgentAsync(runtime);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout));

        Assert.Contains("test failure", error.Message);
        Assert.Equal("observed-model", Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task TelemetryDisabled_SendStillCompletesWithoutActivity()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("github-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime, telemetryEnabled: false);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Empty(capture.Stopped);
    }

    [Fact]
    public async Task Github_ResolvesSelectedModel_BeforeAnySend()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub { SelectedModel = "sdk-default" };
        await using var rig = await CreateAgentAsync(runtime);

        Assert.Equal("sdk-default", rig.Agent.Model);
        Assert.Equal(AgentModelSource.Sdk, rig.Metadata.Current.Source);
        Assert.Equal(1, runtime.ModelQueryCount);
        Assert.False(runtime.CreateOptions.TryGetProperty("model", out _));
        Assert.Empty(capture.Stopped);
        Assert.Empty(rig.Warnings);
    }

    [Fact]
    public async Task Github_UsageOverridesSelectedModel_ButIsNotCachedForTheNextSend()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("actual-model"), Idle()], [Idle()])
        {
            SelectedModel = "sdk-default",
        };
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("first prompt", CancellationToken.None).WaitAsync(TestTimeout);
        await rig.Agent.SendAsync("second prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(new[] { "actual-model", "sdk-default" }, capture.Stopped.Select(span => span.Model));
        Assert.Equal(new[] { "usage", "sdk" }, capture.Stopped.Select(span => span.Source));
        Assert.Equal("sdk-default", rig.Agent.Model);
    }

    [Fact]
    public async Task Github_CancellationBeforeUsage_PreservesTheSelectedModel()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([new AssistantMessageEvent
        {
            Data = new AssistantMessageData { MessageId = "cancel-boundary", Content = "cancel" },
        }]) { SelectedModel = "sdk-default" };
        await using var rig = await CreateAgentAsync(runtime);
        using var cts = new CancellationTokenSource();
        using var cancelOnMessage = rig.Session.On<AssistantMessageEvent>(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => rig.Agent.SendAsync("test prompt", cts.Token).WaitAsync(TestTimeout));

        var stopped = Assert.Single(capture.Stopped);
        Assert.Equal("sdk-default", stopped.Model);
        Assert.Equal("sdk", stopped.Source);
    }

    [Theory]
    [InlineData(null, "changed-model")]
    [InlineData("child", "sdk-default")]
    public async Task Github_TracksOnlyMainSessionModelChanges(string? agentId, string expectedModel)
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Idle()]) { SelectedModel = "sdk-default" };
        await using var rig = await CreateAgentAsync(runtime);
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onChange = rig.Session.On<SessionModelChangeEvent>(_ => processed.TrySetResult());

        await runtime.EmitAsync(new SessionModelChangeEvent
        {
            AgentId = agentId,
            Data = new SessionModelChangeData { NewModel = "changed-model" },
        });
        await processed.Task.WaitAsync(TestTimeout);
        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(expectedModel, rig.Agent.Model);
        Assert.Equal(expectedModel, Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task Github_UsesStartupEvent_WithoutAnExtraQuery()
    {
        await using var runtime = new CopilotRuntimeStub { ModelQueryErrorCode = -32601 };
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onStart = rig.Session.On<SessionStartEvent>(_ => processed.TrySetResult());
        await runtime.EmitAsync(new SessionStartEvent
        {
            Data = new SessionStartData
            {
                SessionId = CopilotRuntimeStub.SessionId, Producer = "test", CopilotVersion = "test",
                StartTime = DateTimeOffset.UtcNow, Version = 1, SelectedModel = "startup-model",
            },
        });
        await processed.Task.WaitAsync(TestTimeout);

        await rig.Metadata.InitializeAsync(rig.Session, CancellationToken.None);

        Assert.Equal("startup-model", rig.Agent.Model);
        Assert.Equal(0, runtime.ModelQueryCount);
        Assert.Empty(rig.Warnings);
    }

    [Fact]
    public async Task Github_ModelChangeDuringQuery_WinsOverTheOlderRpcReply()
    {
        // Ordering invariant 2 of 2: a stale RPC reply cannot overwrite an explicit model change.
        // Guarded by the `IsSelectionChange` precedence in AgentModelTelemetry.InitializeAsync.
        await using var runtime = new CopilotRuntimeStub { SelectedModel = "stale-model" };
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onChange = rig.Session.On<SessionModelChangeEvent>(_ => processed.TrySetResult());
        runtime.BeforeModelResponse = async () =>
        {
            await runtime.EmitAsync(new SessionModelChangeEvent
            {
                Data = new SessionModelChangeData { NewModel = "new-model" },
            });
            await processed.Task.WaitAsync(TestTimeout);
        };

        await rig.Metadata.InitializeAsync(rig.Session, CancellationToken.None);

        Assert.Equal("new-model", rig.Agent.Model);
        Assert.Equal(1, runtime.ModelQueryCount);
        Assert.Empty(rig.Warnings);
    }

    [Fact]
    public async Task Github_LateStartupEvent_DoesNotOverwriteQueriedSelection()
    {
        // Ordering invariant 1 of 2: a startup event delivered after the query resolved is older
        // information, so it must not replace the queried selection. Guarded by the
        // `current.Model is null` precedence in AgentModelTelemetry.Observe.
        await using var runtime = new CopilotRuntimeStub { SelectedModel = "queried-model" };
        await using var rig = await CreateAgentAsync(runtime);
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onStart = rig.Session.On<SessionStartEvent>(_ => processed.TrySetResult());

        await runtime.EmitAsync(Started("old-startup-model"));
        await processed.Task.WaitAsync(TestTimeout);

        Assert.Equal("queried-model", rig.Agent.Model);
    }

    [Fact]
    public async Task Github_StartupEventDuringQuery_DoesNotOverrideTheCurrentRpcSelection()
    {
        // Same ordering seen from the other side: the startup event lands while the query is in
        // flight, so the reply that arrives after it is the newer information and wins.
        //
        // What this test can actually fail on is the ABSENCE of Observe's `current.Model is null`
        // precedence here — add that guard to InitializeAsync and the reply stops replacing the
        // startup observation. It does NOT pin `!current.IsSelectionChange`: make the CAS loop
        // unconditional and this still passes, because the reply overwrites either way. That line
        // is pinned by Github_ModelChangeDuringQuery_WinsOverTheOlderRpcReply instead.
        await using var runtime = new CopilotRuntimeStub { SelectedModel = "queried-model" };
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onStart = rig.Session.On<SessionStartEvent>(_ => processed.TrySetResult());
        runtime.BeforeModelResponse = async () =>
        {
            await runtime.EmitAsync(Started("old-startup-model"));
            await processed.Task.WaitAsync(TestTimeout);
        };

        await rig.Metadata.InitializeAsync(rig.Session, CancellationToken.None);

        Assert.Equal("queried-model", rig.Agent.Model);
    }

    [Theory]
    [InlineData("rpc-error")]
    [InlineData("malformed")]
    [InlineData("null")]
    [InlineData("missing")]
    public async Task Github_MetadataFailure_IsReported_AndUsageCanStillRecoverTheModel(string failure)
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("actual-model"), Idle()]);
        switch (failure)
        {
            case "rpc-error": runtime.ModelQueryErrorCode = -32601; break;
            case "malformed": runtime.ModelResult = () => new { modelId = 42 }; break;
            case "null": runtime.ModelResult = () => null; break;
            case "missing": runtime.ModelResult = () => new { }; break;
            default: throw new ArgumentOutOfRangeException(nameof(failure));
        }
        await using var rig = await CreateAgentAsync(runtime);

        Assert.Equal(string.Empty, rig.Agent.Model);
        Assert.Equal(AgentModelSource.Unavailable, rig.Metadata.Current.Source);
        Assert.Contains("model metadata is unavailable", Assert.Single(rig.Warnings));
        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);
        Assert.Equal("actual-model", Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task ModelQueryHang_DoesNotExceedItsBudget()
    {
        using var capture = new SendCapture();
        // A slow reply, not an absent one: widening the deadline must produce an elapsed time an
        // assertion can fail on, rather than a test only the harness timeout can end.
        await using var runtime = new CopilotRuntimeStub([Usage("actual-model"), Idle()])
        {
            SelectedModel = "too-late-model",
            BeforeModelResponse = () => Task.Delay(SlowModelReply),
        };
        var timer = Stopwatch.StartNew();
        await using var rig = await CreateAgentAsync(runtime);

        Assert.InRange(timer.Elapsed, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6));
        Assert.Contains("timed out", Assert.Single(rig.Warnings));
        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);
        Assert.Equal("actual-model", Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task Github_CallerCancellationDuringModelQuery_IsNotSwallowed()
    {
        await using var runtime = new CopilotRuntimeStub { HoldModelResponse = true };
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        using var cts = new CancellationTokenSource();
        var initialization = rig.Metadata.InitializeAsync(rig.Session, cts.Token);
        await runtime.ModelQueryStarted.Task.WaitAsync(TestTimeout);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization.WaitAsync(TestTimeout));
        Assert.Empty(rig.Warnings);
    }

    private static AssistantUsageEvent Usage(string model, string? agentId = null, string? initiator = null) =>
        new()
        {
            AgentId = agentId,
            Data = new AssistantUsageData { Model = model, Initiator = initiator },
        };

    [Fact]
    public async Task ModelQueryThrows_CallerCancellationStillPropagates()
    {
        // The seam the old ModelQuery_CancelledMidFlight_PropagatesCancellation could not build:
        // the query fails on a torn transport with the caller already cancelled, so the failure
        // reaches the broad catch rather than the cancellation filter above it. Which of the two
        // the transport actually produces is not pinned here — what is pinned is that either one
        // ends as cancellation and never as a reported model failure.
        await using var runtime = new CopilotRuntimeStub { TearTransportOnModelQuery = true };
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        using var cts = new CancellationTokenSource();
        // Runs on the stub's serve loop immediately before it drops the connection.
        runtime.BeforeModelResponse = () =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => rig.Metadata.InitializeAsync(rig.Session, cts.Token).WaitAsync(TestTimeout));

            Assert.True(rig.Warnings.Count == 0,
                "A cancelled caller is not a failed model lookup, so nothing is reported to the user. " +
                $"Reported: {string.Join(" | ", rig.Warnings)}.");
        }
        finally
        {
            // A torn transport cannot acknowledge the SDK's graceful session detach.
            await rig.Client.ForceStopAsync().WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task ModelQueryThrowsUnexpectedType_RecoveryStillRuns()
    {
        // The SDK normalises wire faults before they reach us: an RPC error and a torn connection
        // both arrive as IOException wrapping the real cause, a malformed result as JsonException.
        // Reaching the failure path with a type the old catch did not name therefore takes a
        // non-wire fault, and a disposed session gives one — ObjectDisposedException, which used
        // to escape into ChatAgent.CreateAsync, dispose the client and rethrow, ending the
        // recovery session the user is waiting on. See Github_MetadataFailure_... for the claim
        // this one cannot make with a disposed session: that a later send still names the model.
        await using var runtime = new CopilotRuntimeStub();
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        await rig.Session.DisposeAsync();

        var failure = await Record.ExceptionAsync(
            () => rig.Metadata.InitializeAsync(rig.Session, CancellationToken.None).WaitAsync(TestTimeout));

        Assert.True(failure is null,
            "A read-only metadata query must not be able to end the recovery session: whatever the " +
            "experimental RPC throws has to degrade to an unavailable model. Escaped: " +
            $"{failure?.GetType().FullName}: {failure?.Message}");
        Assert.Equal(AgentModelSource.Unavailable, rig.Metadata.Current.Source);
        Assert.Contains("model metadata is unavailable", Assert.Single(rig.Warnings));
    }

    [Theory]
    [InlineData(128, null, true)]
    [InlineData(129, null, false)]
    [InlineData(0, "model with spaces", false)]
    // $ in .NET also matches before a single trailing newline, so the anchored pattern admitted
    // both of these until it became \z. The second is 129 characters, one of them a control char.
    [InlineData(0, "gpt-5\n", false)]
    [InlineData(-128, null, false)]
    public async Task ModelName_IsBoundedBeforeExport(int length, string? literal, bool exportable)
    {
        using var capture = new SendCapture();
        // A negative length is the "128 valid characters plus a trailing newline" case.
        var model = literal ?? (length < 0 ? new string('m', -length) + "\n" : new string('m', length));
        await using var runtime = new CopilotRuntimeStub([Idle()]) { SelectedModel = model };
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        var exported = Assert.Single(capture.Stopped);
        Assert.True(exported.Model == (exportable ? model : null),
            "agent.model admits 1-128 characters of [A-Za-z0-9._:-] and records anything else as " +
            $"unavailable; a {model.Length}-character name exported as '{exported.Model}'.");
        Assert.Equal(exportable ? "sdk" : "unavailable", exported.Source);
        if (!exportable)
            Assert.Contains("model metadata is unavailable", Assert.Single(rig.Warnings));
    }

    [Theory]
    [InlineData("session.start")]
    [InlineData("session.resume")]
    [InlineData("session.model_change")]
    public void NullStartupData_DoesNotThrowOrEraseSelection(string eventType)
    {
        var metadata = new AgentModelTelemetry(new AgentOptions { Provider = "github" }, _ => { });
        metadata.Observe(Started("selected-model"));

        metadata.Observe(SessionEvent.FromJson($"{{\"type\":\"{eventType}\",\"data\":null}}"));

        Assert.Equal("selected-model", metadata.Current.Model);
    }

    [Fact]
    public async Task NullUsageData_DoesNotLogAnSdkHandlerError()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [SessionEvent.FromJson("""{"type":"assistant.usage","data":null}"""), Idle()])
        {
            SelectedModel = "selected-model",
        };
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("selected-model", Assert.Single(capture.Stopped).Model);
        Assert.Empty(rig.SdkErrors);
    }

    [Fact]
    public async Task ResumeEvent_ProvidesSelectionBeforeInitialization()
    {
        await using var runtime = new CopilotRuntimeStub();
        await using var rig = await CreateAgentAsync(runtime, initializeModel: false);
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = rig.Session.On<SessionResumeEvent>(_ => processed.TrySetResult());
        await runtime.EmitAsync(new SessionResumeEvent
        {
            Data = new SessionResumeData
            {
                EventCount = 1, ResumeTime = DateTimeOffset.UtcNow, SelectedModel = "resumed-model",
            },
        });
        await processed.Task.WaitAsync(TestTimeout);

        await rig.Metadata.InitializeAsync(rig.Session, CancellationToken.None);

        Assert.Equal("resumed-model", rig.Agent.Model);
        Assert.Equal(AgentModelSource.Sdk, rig.Metadata.Current.Source);
        Assert.Equal(0, runtime.ModelQueryCount);
        Assert.Empty(rig.SdkErrors);
    }

    [Fact]
    public async Task CancelledSend_LateUsageBeforeTheNextTurnIsIgnored()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [new AssistantMessageEvent
            {
                Data = new AssistantMessageData { MessageId = "cancel", Content = "cancel" },
            }], [Idle()])
        {
            SelectedModel = "selected-model",
        };
        await using var rig = await CreateAgentAsync(runtime);
        using var cts = new CancellationTokenSource();
        using var cancel = rig.Session.On<AssistantMessageEvent>(_ => cts.Cancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => rig.Agent.SendAsync("first prompt", cts.Token).WaitAsync(TestTimeout));
        runtime.BeforeTurn = () => runtime.EmitAsync(Usage("previous-send-model", initiator: "user"));

        await rig.Agent.SendAsync("next prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("selected-model", capture.Stopped[1].Model);
        Assert.Equal("sdk", capture.Stopped[1].Source);
    }

    [Fact]
    public async Task UsageAfterTurnEnd_IsIgnored_AndLaterTurnsAreCaptured()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [Usage("first-model"), new AssistantTurnEndEvent
            {
                Data = new AssistantTurnEndData { TurnId = "first" },
            }, Usage("late-model"), new AssistantTurnStartEvent
            {
                Data = new AssistantTurnStartData { TurnId = "second" },
            }, Usage("second-model"), new AssistantTurnEndEvent
            {
                Data = new AssistantTurnEndData { TurnId = "second" },
            }, Usage("late-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal("second-model", Assert.Single(capture.Stopped).Model);
    }

    private static SessionIdleEvent Idle() => new() { Data = new SessionIdleData() };

    private static SessionStartEvent Started(string model) => new()
    {
        Data = new SessionStartData
        {
            SessionId = CopilotRuntimeStub.SessionId, Producer = "test", CopilotVersion = "test",
            StartTime = DateTimeOffset.UtcNow, Version = 1, SelectedModel = model,
        },
    };

    private static async Task<AgentRig> CreateAgentAsync(
        CopilotRuntimeStub runtime, string configuredModel = "", bool initializeModel = true,
        bool telemetryEnabled = true)
    {
        var warnings = new List<string>();
        var options = new AgentOptions
        {
            Provider = string.IsNullOrEmpty(configuredModel) ? "github" : "builtin",
            Builtin = new BuiltinProviderOptions { ModelId = configuredModel },
        };
        var metadata = new AgentModelTelemetry(options, warnings.Add);
        var sdkErrors = new ConcurrentQueue<string>();
        var trace = new AgentTraceCapture(telemetryEnabled, listen: false);
        var context = new AgentInvocationContext(null, trace.Telemetry);
        var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForUri(runtime.Url),
            Logger = new SdkErrorLogger(sdkErrors),
        });
        try
        {
            var config = new SessionConfig
            {
                SessionId = CopilotRuntimeStub.SessionId,
                Model = string.IsNullOrEmpty(configuredModel) ? null : configuredModel,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                OnEvent = metadata.Observe,
            };
            context.Bind(config);
            var session = await client.CreateSessionAsync(config).WaitAsync(TestTimeout);
            if (initializeModel)
                await metadata.InitializeAsync(session, CancellationToken.None).WaitAsync(TestTimeout);
            return new AgentRig(new ChatAgent(client, session, metadata, state: null, context),
                client, session, metadata, warnings, sdkErrors, trace);
        }
        catch
        {
            await client.DisposeAsync();
            context.Dispose();
            trace.Dispose();
            throw;
        }
    }

    private sealed record AgentRig(
        ChatAgent Agent, CopilotClient Client, CopilotSession Session, AgentModelTelemetry Metadata, List<string> Warnings,
        ConcurrentQueue<string> SdkErrors, AgentTraceCapture Trace) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await Agent.DisposeAsync().AsTask().WaitAsync(TestTimeout); }
            finally { Trace.Dispose(); }
        }
    }

    private sealed class SdkErrorLogger(ConcurrentQueue<string> errors) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
                errors.Enqueue(formatter(state, exception));
        }
    }

    private sealed class SendCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        public List<(Activity Span, string? Model, string? Source)> Stopped { get; } = [];

        public SendCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Agent",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == "Agent.Send")
                        Stopped.Add((activity, activity.GetTagItem(TelemetryTags.AgentModel)?.ToString(),
                            activity.GetTagItem(TelemetryTags.AgentModelSource)?.ToString()));
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();
    }

    // Exercise the real SDK event queue and ChatAgent.SendAsync without a daemon, credentials, or LLM.
    private sealed class CopilotRuntimeStub : IAsyncDisposable
    {
        public const string SessionId = "model-telemetry-test-session";
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _shutdown = new(TimeSpan.FromSeconds(15));
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private readonly TaskCompletionSource<NetworkStream> _connection =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Queue<SessionEvent[]> _turns;
        private readonly Task _serverTask;

        public CopilotRuntimeStub(params SessionEvent[][] turns)
        {
            _turns = new Queue<SessionEvent[]>(turns);
            _listener.Start();
            _serverTask = ServeAsync();
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        public JsonElement CreateOptions { get; private set; }
        public string? SelectedModel { get; set; }
        public int ModelQueryCount { get; private set; }
        public int? ModelQueryErrorCode { get; set; }
        public bool HoldModelResponse { get; set; }
        // Drops the connection instead of answering the model query, so the client's in-flight
        // request fails on a torn transport rather than on a clean RPC error.
        public bool TearTransportOnModelQuery { get; set; }
        public Func<object?>? ModelResult { get; set; }
        public Func<Task>? BeforeModelResponse { get; set; }
        public Func<Task>? BeforeTurn { get; set; }
        public TaskCompletionSource ModelQueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task EmitAsync(SessionEvent sessionEvent)
        {
            var stream = await _connection.Task.WaitAsync(_shutdown.Token);
            await WriteAsync(stream, new
            {
                jsonrpc = "2.0",
                method = "session.event",
                @params = new
                {
                    sessionId = SessionId,
                    @event = JsonSerializer.Deserialize<JsonElement>(sessionEvent.ToJson()),
                },
            });
        }

        private async Task ServeAsync()
        {
            using var socket = await _listener.AcceptTcpClientAsync(_shutdown.Token);
            await using var stream = socket.GetStream();
            _connection.SetResult(stream);
            while (await ReadAsync(stream, _shutdown.Token) is { } request)
            {
                using (request)
                {
                    var root = request.RootElement;
                    if (!root.TryGetProperty("id", out var id))
                        continue;
                    // Defensive for the same reason the teardown verbs are answered generically:
                    // a message this stub cannot parse must not take down the receive loop and
                    // resurface later against whichever test happens to be tearing down.
                    if (!root.TryGetProperty("method", out var methodElement))
                        continue;
                    var method = methodElement.GetString();
                    if (method == "session.model.getCurrent")
                    {
                        ModelQueryCount++;
                        ModelQueryStarted.TrySetResult();
                        if (BeforeModelResponse is not null)
                            await BeforeModelResponse();
                        // Returning ends the serve loop, which disposes the socket the client is
                        // waiting on. Leaving cleanly keeps teardown quiet: a faulted serve task
                        // would surface in DisposeAsync as a crash unrelated to the assertion.
                        if (TearTransportOnModelQuery)
                            return;
                        if (ModelQueryErrorCode is { } code)
                        {
                            await WriteAsync(stream, new
                            {
                                jsonrpc = "2.0", id, error = new { code, message = "test model query failure" },
                            });
                            continue;
                        }
                        if (HoldModelResponse)
                            continue;
                    }
                    object? result = method switch
                    {
                        "connect" => new { ok = true, protocolVersion = 3, version = "telemetry-test" },
                        "session.create" => CreateSession(root.GetProperty("params")),
                        "session.send" => new { messageId = Guid.NewGuid().ToString() },
                        "session.model.getCurrent" => ModelResult is null
                            ? new { modelId = SelectedModel } : ModelResult(),
                        "session.destroy" => new { },
                        // Teardown is not any of these tests' subject, and the SDK's teardown
                        // sequence is not frozen -- session.detach appeared only once the client
                        // began shutting down cleanly. Answering the teardown verbs generically
                        // keeps a model-telemetry assertion from failing over the shape of a
                        // goodbye, in a stack trace that names neither.
                        "session.detach" or "session.abort" or "session.shutdown" => new { },
                        _ => throw new InvalidOperationException($"Unexpected SDK RPC: {method}"),
                    };
                    await WriteAsync(stream, new { jsonrpc = "2.0", id, result });
                    if (method == "session.send")
                    {
                        if (BeforeTurn is not null)
                            await BeforeTurn();
                        await EmitAsync(new AssistantTurnStartEvent
                        {
                            Data = new AssistantTurnStartData { TurnId = Guid.NewGuid().ToString() },
                        });
                        foreach (var sessionEvent in _turns.Dequeue())
                            await EmitAsync(sessionEvent);
                    }
                }
            }
        }

        private object CreateSession(JsonElement options)
        {
            CreateOptions = options.Clone();
            return new { sessionId = SessionId };
        }

        private async Task WriteAsync(Stream stream, object payload)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(payload);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            await _writeGate.WaitAsync(_shutdown.Token);
            try
            {
                await stream.WriteAsync(header, _shutdown.Token);
                await stream.WriteAsync(body, _shutdown.Token);
                await stream.FlushAsync(_shutdown.Token);
            }
            finally
            {
                _writeGate.Release();
            }
        }

        private static async Task<JsonDocument?> ReadAsync(Stream stream, CancellationToken ct)
        {
            var header = new StringBuilder();
            var oneByte = new byte[1];
            while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (await stream.ReadAsync(oneByte, ct) == 0)
                {
                    if (header.Length == 0)
                        return null;
                    throw new EndOfStreamException("Incomplete RPC header.");
                }
                header.Append((char)oneByte[0]);
                if (header.Length > 4096)
                    throw new InvalidDataException("RPC header is too large.");
            }
            // First, not Single: an LSP-style transport may legitimately carry other headers, and a
            // duplicate or a companion header should not be an exception thrown from a background
            // read loop. TryParse for the same reason.
            var lengthHeader = header.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            if (lengthHeader is null ||
                !int.TryParse(lengthHeader["Content-Length:".Length..], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var length))
            {
                throw new InvalidDataException("RPC frame carried no readable Content-Length.");
            }
            if (length < 0 || length > 1024 * 1024)
                throw new InvalidDataException("Invalid RPC content length.");
            var body = new byte[length];
            await stream.ReadExactlyAsync(body, ct);
            return JsonDocument.Parse(body);
        }

        public async ValueTask DisposeAsync()
        {
            await _shutdown.CancelAsync();
            _listener.Stop();
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
            finally
            {
                _writeGate.Dispose();
                _shutdown.Dispose();
            }
        }
    }
}
