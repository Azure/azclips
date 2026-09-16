// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
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
public sealed class SourceRecordedHierarchyTests
{
    [Fact]
    public void ContextDisposedMidSend_StopsTheAbandonedOperationSoItStillExports()
    {
        // ChatAgent.DisposeAsync gives the SDK five seconds per handle and then gives up. If a send
        // is still in flight when that budget runs out, the context is disposed with an operation
        // still in its slot. Clearing the slot without stopping the activity loses it entirely: an
        // Activity that is never stopped is never exported, so the one turn that hung -- the turn a
        // hang investigation needs -- would be the only turn with no span at all.
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(
            AgentTracing.Source, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", null);
        var context = new AgentInvocationContext(session);

        // Deliberately not disposed: this stands in for a send that never returned.
        var send = context.StartOperation("Agent.Send");
        var activity = send.Activity!;
        Assert.False(activity.IsStopped);

        context.Dispose();

        Assert.True(activity.IsStopped);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);

        // The late send unwinding afterwards must not double-stop or resurrect anything.
        send.Dispose();
        Assert.True(activity.IsStopped);
    }

    [Fact]
    public void CrossProcessSession_UsesRecordedRecoveryIdentityDepthAndPath()
    {
        using var trace = new AgentTraceCapture();
        using var recovery = TelemetryHierarchy.StartActivity(
            AgentTracing.Source, "CLI.Activity.AiRecovery", parentContext: default(ActivityContext));
        Assert.NotNull(recovery);
        AgentTraceCapture.AssertHierarchy(recovery, null, 0, "Agent");
        Assert.Equal("CLI.Activity.AiRecovery", recovery.OperationName);
        var handoff = TelemetryHierarchy.CreateRecoveryHandoff(recovery);
        using var unrelated = TelemetryHierarchy.StartActivity(
            AgentTracing.Source, "unrelated", parentContext: default(ActivityContext));

        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, recovery.Id, handoff);

        Assert.NotNull(session);
        Assert.Equal(recovery.TraceId, session.TraceId);
        Assert.Equal(recovery.SpanId, session.ParentSpanId);
        Assert.True(session.HasRemoteParent);
        AgentTraceCapture.AssertHierarchy(session, recovery, 1, "Agent.Session");
        Assert.Equal("CLI", session.Source.Name);
        using var context = new AgentInvocationContext(session);
        using var create = context.StartOperation("Agent.Create");
        AgentTraceCapture.AssertHierarchy(create.Activity!, session, 2, "Agent.Session.Create");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-traceparent")]
    public void MissingOrInvalidHandoff_StartsAnIndependentSession(string? traceparent)
    {
        using var trace = new AgentTraceCapture();
        using var unrelated = AgentTracing.StartActivity("CLI.Activity.AiRecovery");
        using (var session = RecoveryTraceContext.StartSession(AgentTracing.Source, traceparent, null))
        {
            Assert.NotNull(session);
            Assert.NotEqual(unrelated!.TraceId, session.TraceId);
            AgentTraceCapture.AssertHierarchy(session, null, 0, "Agent.Session");
        }
        Assert.Same(unrelated, Activity.Current);
    }

    [Fact]
    public void LegacyTraceparent_DoesNotInventSourceLineageForSessionOrDescendants()
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(
            AgentTracing.Source, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", null);
        using var context = new AgentInvocationContext(session);
        using var create = context.StartOperation("Agent.Create");
        using var tool = ToolInvocationLog.Start("get_command");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", session!.TraceId.ToHexString());
        Assert.Equal("00f067aa0ba902b7", session.ParentSpanId.ToHexString());
        Assert.Null(session.GetTagItem(TelemetryTags.HierarchyVersion));
        Assert.Null(create.Activity!.GetTagItem(TelemetryTags.HierarchyVersion));
        Assert.Null(Activity.Current!.GetTagItem(TelemetryTags.HierarchyVersion));
    }

    [Theory]
    [InlineData("Version", "2")]
    [InlineData("Depth", "1")]
    [InlineData("Depth", "null")]
    [InlineData("Path", "\"private prompt\"")]
    [InlineData("Path", "\"CLI.Activity.AiRecovery\"")]
    [InlineData("Path", "\"Agent.Session\"")]
    [InlineData("SpanId", "\"1111111111111111\"")]
    [InlineData("TraceId", "\"11111111111111111111111111111111\"")]
    public void InvalidSourceHandoff_IsNotUsedToLabelLegacyParentage(string key, string value)
    {
        using var trace = new AgentTraceCapture();
        using var recovery = AgentTracing.StartActivity("CLI.Activity.AiRecovery");
        var handoff = JsonNode.Parse(TelemetryHierarchy.CreateRecoveryHandoff(recovery)!)!;
        handoff[key] = JsonNode.Parse(value);

        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, recovery!.Id, handoff.ToJsonString());

        Assert.Equal(recovery.TraceId, session!.TraceId);
        Assert.Null(TelemetryHierarchy.Capture(session));
        Assert.Null(session.GetTagItem(TelemetryTags.HierarchyVersion));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Version\":\"1\"}")]
    public void MalformedHandoff_IsRejectedWithoutThrowing(string handoff)
    {
        using var trace = new AgentTraceCapture();
        using var recovery = AgentTracing.StartActivity("CLI.Activity.AiRecovery");
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, recovery!.Id, handoff);
        Assert.Null(session!.GetTagItem(TelemetryTags.HierarchyVersion));
    }

    [Fact]
    public async Task RealToolCallbacks_RebindStaleCreateContextToSendAndReprompt()
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var help = new HelpTools("must-not-run");
        var plan = new ProposePlanTool();
        var config = new SessionConfig { Tools = [help.GetCommand, plan.ProposePlan] };
        context.Bind(config);
        var getCommand = config.Tools!.First().GetService<AIFunction>()!;
        ExecutionContext capturedCreate;
        Activity createActivity;
        using (var create = context.StartOperation("Agent.Create"))
        {
            createActivity = create.Activity!;
            capturedCreate = ExecutionContext.Capture()!;
            // Empty input takes the real tool's validation path without launching az.
            await getCommand.InvokeAsync(new AIFunctionArguments { ["name"] = "" });
        }
        Assert.True(createActivity.IsStopped);

        using var turn = AgentTracing.StartActivity("Agent.Turn");
        Activity sendActivity;
        using (var send = context.StartOperation("Agent.Send"))
        {
            sendActivity = send.Activity!;
            await AgentTraceCapture.InvokeCapturedAsync(
                capturedCreate, getCommand, new AIFunctionArguments { ["name"] = "" });
            await AgentTraceCapture.InvokeCapturedAsync(
                capturedCreate, getCommand, new AIFunctionArguments { ["name"] = "" });
        }

        Activity repromptSend;
        using (var reprompt = AgentTracing.StartActivity("Agent.Reprompt"))
        using (var send = context.StartOperation("Agent.Send"))
        {
            repromptSend = send.Activity!;
            var propose = config.Tools.ElementAt(1).GetService<AIFunction>()!;
            await AgentTraceCapture.InvokeCapturedAsync(capturedCreate, propose,
                new AIFunctionArguments
                {
                    ["issues"] = new[] { "private issue" },
                    ["steps"] = new[] { new StepMeta(1, "private label", "private command") },
                });
        }

        var tools = trace.Stopped.Where(activity => activity.OperationName == "tool.get_command").ToArray();
        Assert.Equal(3, tools.Length);
        AgentTraceCapture.AssertHierarchy(tools[0], createActivity, 2, "Agent.Session.Create.Tool.get_command");
        Assert.All(tools[1..], tool =>
            AgentTraceCapture.AssertHierarchy(tool, sendActivity, 3, "Agent.Session.Turn.Send.Tool.get_command"));
        Assert.Equal(3, tools.Select(tool => tool.SpanId).Distinct().Count());
        var proposed = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.propose_plan");
        AgentTraceCapture.AssertHierarchy(proposed, repromptSend, 4,
            "Agent.Session.Turn.Reprompt.Send.Tool.propose_plan");
        Assert.DoesNotContain("private", (string)proposed.GetTagItem(TelemetryTags.HierarchyPath)!);
        Assert.Same(turn, Activity.Current);
    }

    [Fact]
    public void Binding_PreservesSdkResolutionAndAllToolMetadata()
    {
        using var context = new AgentInvocationContext(null);
        var inner = new ProposePlanTool().ProposePlan;
        var config = new SessionConfig { Tools = [inner] };
        context.Bind(config);

        var bound = Assert.IsAssignableFrom<AIFunction>(Assert.Single(config.Tools!));
        Assert.Same(bound, bound.GetService<AIFunction>());
        Assert.NotSame(inner, bound);
        Assert.Equal(inner.Name, bound.Name);
        Assert.Equal(inner.Description, bound.Description);
        Assert.Equal(inner.JsonSchema.GetRawText(), bound.JsonSchema.GetRawText());
        Assert.Equal(inner.ReturnJsonSchema?.GetRawText(), bound.ReturnJsonSchema?.GetRawText());
        Assert.Same(inner.JsonSerializerOptions, bound.JsonSerializerOptions);
        Assert.Same(inner.UnderlyingMethod, bound.UnderlyingMethod);
        Assert.Same(inner.AdditionalProperties, bound.AdditionalProperties);
    }

    [Fact]
    public async Task HooksAndEvents_RebindCapturedContextWithoutChangingResults()
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var startResult = new SessionStartHookOutput { AdditionalContext = "private startup context" };
        var promptResult = new UserPromptSubmittedHookOutput { AdditionalContext = "private prompt context" };
        var preResult = new PreToolUseHookOutput();
        var invocation = new HookInvocation { SessionId = "offline" };
        var config = new SessionConfig
        {
            Hooks = new SessionHooks
            {
                OnSessionStart = async (_, actual) =>
                {
                    Assert.Same(invocation, actual);
                    using var tool = ToolInvocationLog.Start("hook_start");
                    await Task.Yield();
                    return startResult;
                },
                OnUserPromptSubmitted = async (_, actual) =>
                {
                    Assert.Same(invocation, actual);
                    using var tool = ToolInvocationLog.Start("hook_prompt");
                    await Task.Yield();
                    return promptResult;
                },
                OnPreToolUse = async (_, actual) =>
                {
                    Assert.Same(invocation, actual);
                    using var tool = ToolInvocationLog.Start("hook_pre");
                    await Task.Yield();
                    return preResult;
                },
            },
            OnEvent = _ =>
            {
                using var tool = ToolInvocationLog.Start("hook_event");
            },
        };
        context.Bind(config);
        ExecutionContext captured;
        Activity createActivity;
        using (var create = context.StartOperation("Agent.Create"))
        {
            createActivity = create.Activity!;
            captured = ExecutionContext.Capture()!;
            Assert.Same(startResult, await config.Hooks.OnSessionStart!(new SessionStartHookInput(), invocation));
        }
        using var send = context.StartOperation("Agent.Send");
        Task? callbacks = null;
        ExecutionContext.Run(captured.CreateCopy(), _ => callbacks = InvokeHooksAsync(), null);
        await callbacks!;

        var startup = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.hook_start");
        Assert.Equal(createActivity.SpanId, startup.ParentSpanId);
        foreach (var (name, path) in new[]
        {
            ("tool.hook_prompt", "Agent.Session.Send.Tool.hook_prompt"),
            ("tool.hook_pre", "Agent.Session.Send.Tool.hook_pre"),
            ("tool.hook_event", "Agent.Session.Send.Tool.hook_event"),
        })
        {
            var activity = Assert.Single(trace.Stopped, activity => activity.OperationName == name);
            AgentTraceCapture.AssertHierarchy(activity, send.Activity, 2, path);
            Assert.DoesNotContain("private", (string)activity.GetTagItem(TelemetryTags.HierarchyPath)!);
        }

        async Task InvokeHooksAsync()
        {
            Assert.Same(promptResult, await config.Hooks.OnUserPromptSubmitted!(
                new UserPromptSubmittedHookInput { Prompt = "private prompt" }, invocation));
            Assert.Same(preResult, await config.Hooks.OnPreToolUse!(new PreToolUseHookInput(), invocation));
            config.OnEvent(new SessionIdleEvent { Data = new SessionIdleData() });
            Assert.Same(createActivity, Activity.Current);
        }
    }

    [Fact]
    public async Task NestedBoundTools_KeepTheImmediateToolParent()
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var inner = new HelpTools("must-not-run").GetCommand;
        AIFunction? boundInner = null;
        var outer = AIFunctionFactory.Create(async () =>
        {
            using var scope = ToolInvocationLog.Start("outer");
            return await boundInner!.InvokeAsync(new AIFunctionArguments { ["name"] = "" });
        });
        var config = new SessionConfig { Tools = [inner, outer] };
        context.Bind(config);
        boundInner = config.Tools!.First().GetService<AIFunction>()!;
        using var send = context.StartOperation("Agent.Send");

        await config.Tools.Last().GetService<AIFunction>()!.InvokeAsync(new AIFunctionArguments());

        var outerSpan = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.outer");
        var innerSpan = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
        AgentTraceCapture.AssertHierarchy(outerSpan, send.Activity, 2, "Agent.Session.Send.Tool.outer");
        AgentTraceCapture.AssertHierarchy(innerSpan, outerSpan, 3, "Agent.Session.Send.Tool.outer.Tool.get_command");
    }

    [Fact]
    public async Task InFlightCallback_DoesNotAdoptALaterSendsParentOrCancellation()
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AIFunction? nested = null;
        var outer = AIFunctionFactory.Create(async () =>
        {
            using var scope = ToolInvocationLog.Start("outer");
            entered.SetResult();
            await release.Task;
            return await nested!.InvokeAsync(new AIFunctionArguments { ["name"] = "" });
        });
        var config = new SessionConfig { Tools = [new HelpTools("must-not-run").GetCommand, outer] };
        context.Bind(config);
        nested = config.Tools!.First().GetService<AIFunction>()!;
        using var first = context.StartOperation("Agent.Send");
        var call = config.Tools.Last().GetService<AIFunction>()!.InvokeAsync(new AIFunctionArguments()).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        first.Dispose();
        using var laterCancellation = new CancellationTokenSource();
        laterCancellation.Cancel();
        using var later = context.StartOperation("Agent.Send", laterCancellation.Token);

        release.SetResult();
        await call.WaitAsync(TimeSpan.FromSeconds(10));

        var outerSpan = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.outer");
        var nestedSpan = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
        Assert.Equal(first.Activity!.SpanId, outerSpan.ParentSpanId);
        AgentTraceCapture.AssertHierarchy(nestedSpan, outerSpan, 3, "Agent.Session.Send.Tool.outer.Tool.get_command");
        Assert.NotEqual(later.Activity!.SpanId, nestedSpan.ParentSpanId);
        Assert.Same(later.Activity, Activity.Current);
    }

    [Fact]
    public async Task ParallelToolsAndIndependentSessions_KeepExplicitParentsAndNestedWork()
    {
        using var trace = new AgentTraceCapture();
        using var recovery = AgentTracing.StartActivity("CLI.Activity.AiRecovery");
        var handoff = TelemetryHierarchy.CreateRecoveryHandoff(recovery);
        using var sessionA = RecoveryTraceContext.StartSession(AgentTracing.Source, recovery!.Id, handoff);
        using var contextA = new AgentInvocationContext(sessionA);
        using var sessionB = RecoveryTraceContext.StartSession(AgentTracing.Source, recovery.Id, handoff);
        using var contextB = new AgentInvocationContext(sessionB);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new ConcurrentDictionary<string, Activity>();
        int count = 0;
        var inner = AIFunctionFactory.Create(async (string key, CancellationToken ct) =>
        {
            using var tool = ToolInvocationLog.Start("parallel", "private args");
            observed[key] = Activity.Current!;
            if (Interlocked.Increment(ref count) == 4) entered.SetResult();
            await release.Task.WaitAsync(ct);
            using var nested = AgentTracing.StartActivity("MsalAuthService.AcquireTokenSilentlyAsync");
            AgentTraceCapture.AssertHierarchy(nested!, observed[key], 4,
                "Agent.Session.Send.Tool.parallel.MsalAuthService.AcquireTokenSilentlyAsync");
            return key;
        });
        var configA = new SessionConfig { Tools = [inner] };
        var configB = new SessionConfig { Tools = [inner] };
        contextA.Bind(configA);
        contextB.Bind(configB);
        using var sendA = contextA.StartOperation("Agent.Send");
        using var sendB = contextB.StartOperation("Agent.Send");
        var functionA = configA.Tools!.Single().GetService<AIFunction>()!;
        var functionB = configB.Tools!.Single().GetService<AIFunction>()!;
        var calls = new[]
        {
            functionA.InvokeAsync(new AIFunctionArguments { ["key"] = "a1" }).AsTask(),
            functionA.InvokeAsync(new AIFunctionArguments { ["key"] = "a2" }).AsTask(),
            functionB.InvokeAsync(new AIFunctionArguments { ["key"] = "b1" }).AsTask(),
            functionB.InvokeAsync(new AIFunctionArguments { ["key"] = "b2" }).AsTask(),
        };
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.All(observed.Where(pair => pair.Key.StartsWith('a')), pair =>
                Assert.Equal(sendA.Activity!.SpanId, pair.Value.ParentSpanId));
            Assert.All(observed.Where(pair => pair.Key.StartsWith('b')), pair =>
                Assert.Equal(sendB.Activity!.SpanId, pair.Value.ParentSpanId));
            Assert.Equal(4, observed.Values.Select(activity => activity.SpanId).Distinct().Count());
            Assert.Equal(sessionA!.SpanId, sendA.Activity!.ParentSpanId);
            Assert.Equal(sessionB!.SpanId, sendB.Activity!.ParentSpanId);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(calls);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExceptionAndCancellation_ReleaseBindingsAndKeepLaterSendsCorrect(bool cancel)
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = AIFunctionFactory.Create((Func<CancellationToken, Task<string>>)(async ct =>
        {
            using var tool = ToolInvocationLog.Start("failure");
            entered.TrySetResult();
            await Task.Yield();
            if (cancel)
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("expected test failure");
        }));
        var config = new SessionConfig { Tools = [inner] };
        context.Bind(config);
        var bound = config.Tools!.Single().GetService<AIFunction>()!;
        using (context.StartOperation("Agent.Send", cancellation.Token))
        {
            var invocation = bound.InvokeAsync(new AIFunctionArguments()).AsTask();
            await entered.Task;
            cancellation.Cancel();
            if (cancel)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
            else
                await Assert.ThrowsAsync<InvalidOperationException>(() => invocation);
        }
        Assert.Same(session, Activity.Current);
        using (var later = context.StartOperation("Agent.Send"))
        {
            Assert.Equal(session!.SpanId, later.Activity!.ParentSpanId);
            Assert.Throws<InvalidOperationException>(() => context.StartOperation("Agent.Send"));
        }
        context.Dispose();
        context.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bound.InvokeAsync(new AIFunctionArguments()).AsTask());
        Assert.Throws<ObjectDisposedException>(() => context.StartOperation("Agent.Send"));
        Assert.Same(session, Activity.Current);
        Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.failure");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolCancellation_HonorsBothOperationAndCallbackTokens(bool cancelCallback)
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        using var operationCancellation = new CancellationTokenSource();
        using var callbackCancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var function = AIFunctionFactory.Create(async (CancellationToken ct) =>
        {
            using var tool = ToolInvocationLog.Start("wait");
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return "";
        });
        var config = new SessionConfig { Tools = [function] };
        context.Bind(config);
        using var send = context.StartOperation("Agent.Send", operationCancellation.Token);
        var call = config.Tools!.Single().GetService<AIFunction>()!
            .InvokeAsync(new AIFunctionArguments(), callbackCancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        (cancelCallback ? callbackCancellation : operationCancellation).Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Same(send.Activity, Activity.Current);
    }

    [Fact]
    public async Task IdleCallbacks_UseSessionInsteadOfCompletedCreate()
    {
        using var trace = new AgentTraceCapture();
        using var session = RecoveryTraceContext.StartSession(AgentTracing.Source, null, null);
        using var context = new AgentInvocationContext(session);
        var config = new SessionConfig { Tools = [new HelpTools("must-not-run").GetCommand] };
        context.Bind(config);
        ExecutionContext captured;
        using (context.StartOperation("Agent.Create"))
            captured = ExecutionContext.Capture()!;

        await AgentTraceCapture.InvokeCapturedAsync(
            captured, config.Tools!.Single().GetService<AIFunction>()!, new AIFunctionArguments { ["name"] = "" });

        var tool = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
        AgentTraceCapture.AssertHierarchy(tool, session, 1, "Agent.Session.Tool.get_command");
    }

    [Fact]
    public async Task NoTelemetryListener_StillExecutesToolsWithoutSpansOrLeakedAmbientState()
    {
        Assert.False(AgentTracing.Source.HasListeners());
        using var context = new AgentInvocationContext(null);
        var config = new SessionConfig { Tools = [new HelpTools("must-not-run").GetCommand] };
        context.Bind(config);
        using var operation = context.StartOperation("Agent.Send");

        var result = await config.Tools!.Single().GetService<AIFunction>()!
            .InvokeAsync(new AIFunctionArguments { ["name"] = "" });

        Assert.Null(operation.Activity);
        Assert.Contains("missing_name", result!.ToString());
        Assert.Null(Activity.Current);
    }

    [Fact]
    public void ToolScope_DisposesOnlyOnce()
    {
        using var trace = new AgentTraceCapture();
        var name = "dispose_" + Guid.NewGuid().ToString("N");
        var scope = ToolInvocationLog.Start(name);
        scope.Dispose();
        scope.Dispose();
        Assert.Single(trace.Stopped);
        Assert.Single(ToolInvocationLog.Snapshot(), invocation => invocation.Name == name);
    }
}

internal sealed class AgentTraceCapture : IDisposable
{
    private readonly ActivityListener _listener;
    internal ConcurrentQueue<Activity> Stopped { get; } = new();

    internal AgentTraceCapture()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    internal static void AssertHierarchy(Activity activity, Activity? parent, int depth, string path)
    {
        Assert.Equal(1, activity.GetTagItem(TelemetryTags.HierarchyVersion));
        Assert.Equal(activity.SpanId.ToHexString(), activity.GetTagItem(TelemetryTags.HierarchySpanId));
        Assert.Equal(parent?.SpanId.ToHexString() ?? "", activity.GetTagItem(TelemetryTags.HierarchyParentSpanId));
        Assert.Equal(parent?.SpanId ?? default, activity.ParentSpanId);
        Assert.Equal(depth, activity.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal(path, activity.GetTagItem(TelemetryTags.HierarchyPath));
    }

    internal static Task<object?> InvokeCapturedAsync(
        ExecutionContext captured, AIFunction function, AIFunctionArguments arguments)
    {
        Task<object?>? invocation = null;
        ExecutionContext.Run(captured.CreateCopy(), _ => invocation = InvokeAndCheckAsync(), null);
        return invocation!;

        async Task<object?> InvokeAndCheckAsync()
        {
            var previous = Activity.Current;
            try
            {
                return await function.InvokeAsync(arguments);
            }
            finally
            {
                Assert.Same(previous, Activity.Current);
            }
        }
    }

    public void Dispose() => _listener.Dispose();
}
