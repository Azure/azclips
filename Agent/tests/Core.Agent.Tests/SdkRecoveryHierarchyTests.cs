// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        server.ThrowIfFaulted();
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
        server.ThrowIfFaulted();
    }

    [Fact]
    public async Task RejectedOverlappingSend_LeavesTheLiveSendCancellable()
    {
        // A second SendAsync loses the race for the single operation slot and is rejected. It must
        // leave nothing of itself behind: if it can replace what the live send published, Esc and
        // disposal both go nowhere and the turn runs to completion uncancelled.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // Deliberately not `timeout`. When the live send and the test's own waits shared one token,
        // a reverted fix still ended the live send -- via that shared token, once the shared budget
        // expired. The test went red on a timeout rather than on an assertion, and was recorded as
        // validated. These two must stay distinct or this test stops testing anything.
        using var liveBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.NotEqual(liveBudget.Token, timeout.Token);
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

        var live = agent.SendAsync("hold the slot", liveBudget.Token);
        await entered.Task.WaitAsync(timeout.Token);

        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.SendAsync("overlap", timeout.Token));
        Assert.Contains("one in-flight operation", rejected.Message);

        // The live send must still answer to abort, and it must do so on abort's own budget -- not
        // on its own, which is why the two tokens above are unrelated.
        var sw = Stopwatch.StartNew();
        await agent.AbortAsync(timeout.Token);
        var settled = await Task.WhenAny(live, Task.Delay(TimeSpan.FromSeconds(2), timeout.Token));
        sw.Stop();
        Assert.True(ReferenceEquals(settled, live),
            $"The live send was still running {sw.Elapsed} after AbortAsync returned: the rejected "
            + "overlapping send disarmed the turn that owns the slot.");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"AbortAsync took {sw.Elapsed} to stop the live send.");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => live);
        server.ThrowIfFaulted();
    }

    [Fact]
    public async Task OfflineServerFault_FailsTheProvokingTest()
    {
        // The receive loop runs detached, so a message it cannot handle used to be invisible until
        // shutdown and was then blamed on an unrelated test. It must be observable by the test that
        // provoked it, at the moment that test asks.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = new OfflineCopilotServer();
        await using (server)
        {
            using var probe = new TcpClient();
            await probe.ConnectAsync(IPAddress.Loopback, int.Parse(server.Url.Split(':')[^1]), timeout.Token);
            await using var stream = probe.GetStream();
            var payload = """{"jsonrpc":"2.0","id":1,"method":"offline.fault.probe"}"""u8.ToArray();
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n"), timeout.Token);
            await stream.WriteAsync(payload, timeout.Token);
            await stream.FlushAsync(timeout.Token);
            await server.Faulted.WaitAsync(timeout.Token);
        }

        Exception? surfaced = null;
        try { server.ThrowIfFaulted(); }
        catch (InvalidOperationException ex) { surfaced = ex; }

        Assert.NotNull(surfaced);
        Assert.Contains("offline.fault.probe", surfaced.InnerException!.Message, StringComparison.Ordinal);
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
        server.ThrowIfFaulted();
    }
}
