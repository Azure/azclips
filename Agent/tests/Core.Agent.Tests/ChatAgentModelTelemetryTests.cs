// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Telemetry;
using GitHub.Copilot;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public class ChatAgentModelTelemetryTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Github_RecordsSdkModelBeforeSendStops_WithoutSelectingAModel()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("github-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);
        using var parent = new Activity("test-parent").Start();

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        var stopped = Assert.Single(capture.Stopped);
        Assert.Equal("github-model", stopped.Model);
        Assert.Equal(parent.SpanId, stopped.Span.ParentSpanId);
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
    public async Task Github_EmptyUsageDoesNotEraseObservedModel(string emptyModel)
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub(
            [Usage("main-model"), Usage(emptyModel), Idle()]);
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
        Assert.False(AgentTracing.Source.HasListeners());
        await using var runtime = new CopilotRuntimeStub([Usage("github-model"), Idle()]);
        await using var rig = await CreateAgentAsync(runtime);

        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);

        Assert.False(AgentTracing.Source.HasListeners());
    }

    [Fact]
    public async Task Github_ResolvesSelectedModel_BeforeAnySend()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub { SelectedModel = "sdk-default" };
        await using var rig = await CreateAgentAsync(runtime);

        Assert.Equal("sdk-default", rig.Agent.Model);
        Assert.Equal("sdk", rig.Metadata.Current.Source);
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
        Assert.Equal("unavailable", rig.Metadata.Current.Source);
        Assert.Contains("model metadata is unavailable", Assert.Single(rig.Warnings));
        await rig.Agent.SendAsync("test prompt", CancellationToken.None).WaitAsync(TestTimeout);
        Assert.Equal("actual-model", Assert.Single(capture.Stopped).Model);
    }

    [Fact]
    public async Task Github_MetadataTimeout_IsBounded_AndDoesNotBlockRecovery()
    {
        using var capture = new SendCapture();
        await using var runtime = new CopilotRuntimeStub([Usage("actual-model"), Idle()])
        {
            HoldModelResponse = true,
        };
        var timer = Stopwatch.StartNew();
        await using var rig = await CreateAgentAsync(runtime);

        Assert.InRange(timer.Elapsed, TimeSpan.FromSeconds(4), TestTimeout);
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
        CopilotRuntimeStub runtime, string configuredModel = "", bool initializeModel = true)
    {
        var warnings = new List<string>();
        var options = new AgentOptions
        {
            Provider = string.IsNullOrEmpty(configuredModel) ? "github" : "builtin",
            Builtin = new BuiltinProviderOptions { ModelId = configuredModel },
        };
        var metadata = new AgentModelTelemetry(options, warnings.Add);
        var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForUri(runtime.Url),
        });
        try
        {
            var session = await client.CreateSessionAsync(new SessionConfig
            {
                SessionId = CopilotRuntimeStub.SessionId,
                Model = string.IsNullOrEmpty(configuredModel) ? null : configuredModel,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                OnEvent = metadata.Observe,
            }).WaitAsync(TestTimeout);
            if (initializeModel)
                await metadata.InitializeAsync(session, CancellationToken.None).WaitAsync(TestTimeout);
            return new AgentRig(new ChatAgent(client, session, metadata, state: null), session, metadata, warnings);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private sealed record AgentRig(
        ChatAgent Agent, CopilotSession Session, AgentModelTelemetry Metadata, List<string> Warnings) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Agent.DisposeAsync();
    }

    private sealed class SendCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        public List<(Activity Span, string? Model, string? Source)> Stopped { get; } = [];

        public SendCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentTracing.SourceName,
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
        public Func<object?>? ModelResult { get; set; }
        public Func<Task>? BeforeModelResponse { get; set; }
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
                    var method = root.GetProperty("method").GetString();
                    if (method == "session.model.getCurrent")
                    {
                        ModelQueryCount++;
                        ModelQueryStarted.TrySetResult();
                        if (BeforeModelResponse is not null)
                            await BeforeModelResponse();
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
                        _ => throw new InvalidOperationException($"Unexpected SDK RPC: {method}"),
                    };
                    await WriteAsync(stream, new { jsonrpc = "2.0", id, result });
                    if (method == "session.send")
                    {
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
            var lengthHeader = header.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var length = int.Parse(lengthHeader["Content-Length:".Length..], CultureInfo.InvariantCulture);
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
