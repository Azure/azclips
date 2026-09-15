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

    private static AssistantUsageEvent Usage(string model, string? agentId = null, string? initiator = null) =>
        new()
        {
            AgentId = agentId,
            Data = new AssistantUsageData { Model = model, Initiator = initiator },
        };

    private static SessionIdleEvent Idle() => new() { Data = new SessionIdleData() };

    private static async Task<AgentRig> CreateAgentAsync(CopilotRuntimeStub runtime, string configuredModel = "")
    {
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
            }).WaitAsync(TestTimeout);
            return new AgentRig(new ChatAgent(client, session, configuredModel, state: null), session);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private sealed record AgentRig(ChatAgent Agent, CopilotSession Session) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Agent.DisposeAsync();
    }

    private sealed class SendCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        public List<(Activity Span, string? Model)> Stopped { get; } = [];

        public SendCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentTracing.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == "Agent.Send")
                        Stopped.Add((activity, activity.GetTagItem(TelemetryTags.AgentModel)?.ToString()));
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
                    object result = method switch
                    {
                        "connect" => new { ok = true, protocolVersion = 3, version = "telemetry-test" },
                        "session.create" => CreateSession(root.GetProperty("params")),
                        "session.send" => new { messageId = Guid.NewGuid().ToString() },
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
