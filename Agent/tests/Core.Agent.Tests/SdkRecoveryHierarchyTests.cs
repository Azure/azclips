// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Hosting;
using Core.Agent.Telemetry;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public sealed class SdkRecoveryHierarchyTests
{
    [Fact]
    public async Task RealSdkDispatch_ParentsRegisteredToolsToChatAgentSendNotCreate()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var trace = new AgentTraceCapture();
        await using var server = new OfflineCopilotServer
        {
            Calls =
            [
                new("get_command", new { name = "" }),
                new("get_command", new { name = "" }),
                new("propose_plan", new
                {
                    issues = new[] { "private issue" },
                    steps = new[] { new { id = 1, label = "private label", cmd = "private command" } },
                }),
            ],
        };
        using var recovery = AgentTracing.StartActivity("CLI.Activity.AiRecovery");
        using var session = RecoveryTraceContext.StartSession(
            AgentTracing.Source, recovery!.Id, TelemetryHierarchy.CreateRecoveryHandoff(recovery));
        using var context = new AgentInvocationContext(session);
        var client = new CopilotClient(new CopilotClientOptions { Connection = RuntimeConnection.ForUri(server.Url) });
        var config = new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Tools = [new HelpTools("must-not-run").GetCommand, new ProposePlanTool().ProposePlan],
        };
        ChatAgent agent;
        Activity createActivity;
        using (var create = context.StartOperation("Agent.Create"))
        {
            createActivity = create.Activity!;
            agent = await ChatAgent.ConnectAsync(client, config, "offline-model", null, context, timeout.Token);
        }
        await using var agentScope = agent;
        Assert.True(createActivity.IsStopped);

        using var turn = AgentTracing.StartActivity("Agent.Turn");
        await agent.SendAsync("private prompt", timeout.Token);

        Assert.Equal(3, server.ToolResults.Count);
        var send = Assert.Single(trace.Stopped, activity => activity.OperationName == "Agent.Send");
        AgentTraceCapture.AssertHierarchy(send, turn, 3, "Agent.Session.Turn.Send");
        var tools = trace.Stopped.Where(activity => activity.OperationName.StartsWith("tool.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, tools.Length);
        Assert.All(tools, tool =>
        {
            Assert.Equal(recovery.TraceId, tool.TraceId);
            Assert.NotEqual(createActivity.SpanId, tool.ParentSpanId);
        });
        var lookups = tools.Where(tool => tool.OperationName == "tool.get_command").ToArray();
        Assert.Equal(2, lookups.Length);
        Assert.All(lookups, tool =>
            AgentTraceCapture.AssertHierarchy(tool, send, 4, "Agent.Session.Turn.Send.Tool.get_command"));
        var plan = Assert.Single(tools, tool => tool.OperationName == "tool.propose_plan");
        AgentTraceCapture.AssertHierarchy(plan, send, 4, "Agent.Session.Turn.Send.Tool.propose_plan");
        Assert.Equal(3, tools.Select(activity => activity.SpanId).Distinct().Count());
        Assert.Equal("offline-model", send.GetTagItem(TelemetryTags.AgentModel));
        Assert.Equal("CLI", send.Source.Name);
        Assert.Same(turn, Activity.Current);
    }

    [Fact]
    public async Task RealSdkSendFailureAndDispose_ClearOperationState()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var trace = new AgentTraceCapture();
        await using var server = new OfflineCopilotServer { RejectNextSend = true };
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var client = new CopilotClient(new CopilotClientOptions { Connection = RuntimeConnection.ForUri(server.Url) });
        ChatAgent agent;
        using (context.StartOperation("Agent.Create"))
        {
            agent = await ChatAgent.ConnectAsync(client, new SessionConfig
            {
                SessionId = Guid.NewGuid().ToString(),
                OnPermissionRequest = PermissionHandler.ApproveAll,
            }, "", null, context, timeout.Token);
        }
        await using var agentScope = agent;

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => agent.SendAsync("first", timeout.Token));
        Assert.Contains("offline send failure", failure.Message);
        Assert.Same(session, Activity.Current);
        await agent.SendAsync("second", timeout.Token);
        var sends = trace.Stopped.Where(activity => activity.OperationName == "Agent.Send").ToArray();
        Assert.Equal(2, sends.Length);
        Assert.All(sends, send => AgentTraceCapture.AssertHierarchy(send, session, 1, "Agent.Session.Send"));
        Assert.NotEqual(sends[0].SpanId, sends[1].SpanId);

        await agent.DisposeAsync();
        await agent.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => agent.SendAsync("after disposal", timeout.Token));
        Assert.Equal(2, server.SendCount);
        Assert.Same(session, Activity.Current);
    }

    [Fact]
    public async Task RejectedOverlappingSend_LeavesTheLiveSendCancellable()
    {
        // SendAsync publishes its cancellation handle before reserving the single operation slot,
        // because publishing later leaves an Esc window where an abort finds nothing. The cost of
        // that order is this case: a second SendAsync publishes its own handle and only then gets
        // rejected for the busy slot. If its exit path cleared the field instead of restoring what
        // it displaced, the rejected call would disarm the send that is still running -- Esc and
        // disposal would both go nowhere, and the turn would run to completion uncancelled.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var trace = new AgentTraceCapture();
        await using var server = new OfflineCopilotServer { Calls = [new("wait", new { })] };
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = AIFunctionFactory.Create(async (CancellationToken ct) =>
        {
            using var tool = ToolInvocationLog.Start("wait");
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return "";
        }, new AIFunctionFactoryOptions { Name = "wait" });
        var client = new CopilotClient(new CopilotClientOptions { Connection = RuntimeConnection.ForUri(server.Url) });
        ChatAgent agent;
        using (context.StartOperation("Agent.Create"))
        {
            agent = await ChatAgent.ConnectAsync(client, new SessionConfig
            {
                SessionId = Guid.NewGuid().ToString(),
                OnPermissionRequest = PermissionHandler.ApproveAll,
                Tools = [wait],
            }, "", null, context, timeout.Token);
        }
        await using var agentScope = agent;

        var live = agent.SendAsync("hold the slot", timeout.Token);
        await entered.Task.WaitAsync(timeout.Token);

        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.SendAsync("overlap", timeout.Token));
        Assert.Contains("one in-flight operation", rejected.Message);

        // The live send must still answer to abort. Without the restore this hangs until the
        // 15s budget expires, because the handle it would have cancelled was set to null.
        await agent.AbortAsync(timeout.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => live).WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task RealSdkCancellation_ReachesToolAndReleasesSendScope()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var trace = new AgentTraceCapture();
        await using var server = new OfflineCopilotServer { Calls = [new("wait", new { })] };
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = AIFunctionFactory.Create(async (CancellationToken ct) =>
        {
            using var tool = ToolInvocationLog.Start("wait");
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return "";
            }
            finally
            {
                exited.SetResult();
            }
        }, new AIFunctionFactoryOptions { Name = "wait" });
        var client = new CopilotClient(new CopilotClientOptions { Connection = RuntimeConnection.ForUri(server.Url) });
        ChatAgent agent;
        using (context.StartOperation("Agent.Create"))
        {
            agent = await ChatAgent.ConnectAsync(client, new SessionConfig
            {
                SessionId = Guid.NewGuid().ToString(),
                OnPermissionRequest = PermissionHandler.ApproveAll,
                Tools = [wait],
            }, "", null, context, timeout.Token);
        }
        await using var agentScope = agent;
        var sending = agent.SendAsync("cancel this turn", cancel.Token);
        await entered.Task.WaitAsync(timeout.Token);

        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        await exited.Task.WaitAsync(timeout.Token);
        await server.FirstToolHandled.WaitAsync(timeout.Token);
        var toolSpan = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.wait");
        var send = Assert.Single(trace.Stopped, activity => activity.OperationName == "Agent.Send");
        AgentTraceCapture.AssertHierarchy(toolSpan, send, 2, "Agent.Session.Send.Tool.wait");
        Assert.Same(session, Activity.Current);
    }

    // The real SDK connects only to this loopback JSON-RPC peer. No native daemon,
    // credentials, model, Azure endpoint, or SDK private/reflection API is involved.
    private sealed class OfflineCopilotServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _run;
        private readonly TaskCompletionSource _firstToolHandled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _sessionId = "";
        private int _pending;

        internal OfflineCopilotServer()
        {
            _listener.Start();
            Url = $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _run = RunAsync();
        }

        internal string Url { get; }
        internal ToolCall[] Calls { get; init; } = [];
        internal List<JsonElement> ToolResults { get; } = [];
        internal bool RejectNextSend { get; set; }
        internal int SendCount { get; private set; }
        internal Task FirstToolHandled => _firstToolHandled.Task;

        private async Task RunAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            await using var stream = client.GetStream();
            while (!_stop.IsCancellationRequested)
            {
                using var message = await ReadAsync(stream, _stop.Token);
                if (message is null)
                    return;
                var request = message.RootElement;
                var method = request.GetProperty("method").GetString();
                if (!request.TryGetProperty("id", out var idElement))
                {
                    // A JSON-RPC notification: no id, no reply expected. The SDK sends these on the
                    // abort path, and this fake has nothing to do with any of them. Reading id
                    // unconditionally made one crash the receive loop, and that crash only surfaced
                    // at server shutdown -- the same trap session.detach fell into.
                    continue;
                }
                var id = idElement.Clone();
                object result;
                switch (method)
                {
                    case "connect":
                        result = new { ok = true, protocolVersion = 3, version = "offline-test" };
                        break;
                    case "session.create":
                        _sessionId = request.GetProperty("params").GetProperty("sessionId").GetString()!;
                        result = new { sessionId = _sessionId };
                        break;
                    case "session.send":
                        SendCount++;
                        if (RejectNextSend)
                        {
                            RejectNextSend = false;
                            await WriteAsync(stream, new
                            {
                                jsonrpc = "2.0", id,
                                error = new { code = -32603, message = "offline send failure" },
                            });
                            continue;
                        }
                        await ReplyAsync(stream, id, new { messageId = Guid.NewGuid().ToString() });
                        _pending = Calls.Length;
                        foreach (var call in Calls)
                        {
                            await EventAsync(stream, "external_tool.requested", new
                            {
                                sessionId = _sessionId,
                                requestId = Guid.NewGuid().ToString(),
                                toolCallId = Guid.NewGuid().ToString(),
                                toolName = call.Name,
                                arguments = call.Arguments,
                            });
                        }
                        if (_pending == 0)
                            await EventAsync(stream, "session.idle", new { });
                        continue;
                    case "session.tools.handlePendingToolCall":
                        ToolResults.Add(request.GetProperty("params").Clone());
                        _firstToolHandled.TrySetResult();
                        await ReplyAsync(stream, id, new { });
                        if (--_pending == 0)
                            await EventAsync(stream, "session.idle", new { });
                        continue;
                    case "session.destroy":
                    case "session.abort":
                    // Teardown is not this test's subject, and the SDK's teardown sequence is not
                    // frozen: session.detach appeared here only once ChatAgent.DisposeAsync started
                    // cancelling the in-flight turn, which let disposal run to completion instead of
                    // timing out part-way through. Answering the teardown verbs generically keeps a
                    // span-parentage test from failing over the shape of a goodbye.
                    case "session.detach":
                        result = new { };
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected offline SDK request: {method} :: {request.GetRawText()}");
                }
                await ReplyAsync(stream, id, result);
            }
        }

        private Task ReplyAsync(Stream stream, JsonElement id, object result) =>
            WriteAsync(stream, new { jsonrpc = "2.0", id, result });

        private Task EventAsync(Stream stream, string type, object data) =>
            WriteAsync(stream, new
            {
                jsonrpc = "2.0",
                method = "session.event",
                @params = new
                {
                    sessionId = _sessionId,
                    @event = new
                    {
                        id = Guid.NewGuid().ToString(),
                        timestamp = DateTimeOffset.UtcNow,
                        parentId = (string?)null,
                        type,
                        data,
                    },
                },
            });

        private async Task WriteAsync(Stream stream, object value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n"), _stop.Token);
            await stream.WriteAsync(bytes, _stop.Token);
            await stream.FlushAsync(_stop.Token);
        }

        private static async Task<JsonDocument?> ReadAsync(Stream stream, CancellationToken ct)
        {
            var header = new List<byte>();
            var next = new byte[1];
            while (header.Count < 8192)
            {
                if (await stream.ReadAsync(next, ct) == 0)
                {
                    if (header.Count == 0)
                        return null;
                    throw new EndOfStreamException("Incomplete offline RPC header.");
                }
                header.Add(next[0]);
                if (header.Count >= 4 && header.TakeLast(4).SequenceEqual("\r\n\r\n"u8.ToArray()))
                    break;
            }
            var lengthHeader = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n")
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var length = int.Parse(lengthHeader["Content-Length:".Length..], CultureInfo.InvariantCulture);
            var body = new byte[length];
            await stream.ReadExactlyAsync(body, ct);
            return JsonDocument.Parse(body);
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            try
            {
                await _run.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex) when (_stop.IsCancellationRequested
                                       && ex is OperationCanceledException or SocketException or IOException)
            {
            }
            finally
            {
                _stop.Dispose();
            }
        }
    }

    private sealed record ToolCall(string Name, object Arguments);
}
