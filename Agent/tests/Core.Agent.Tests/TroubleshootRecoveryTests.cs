// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot;
using Xunit;
using static Core.Agent.Tests.TroubleshootTestSupport;

namespace Core.Agent.Tests;

[Collection("Troubleshoot")]
public class TroubleshootRecoveryTests
{
    [Theory]
    [InlineData("manual", "oneshot")]
    [InlineData("manual", "interactive")]
    [InlineData("auto", "oneshot")]
    [InlineData("auto", "interactive")]
    public void Recovery_UsesTheNewTriggerAndSharedStyle(string trigger, string style)
    {
        var options = new AgentOptions { Recovery = new() { Trigger = trigger, Style = style } };
        TroubleshootRecoverProcessor.RequireRecovery(options, null, false, false, false);
        TroubleshootRecoverProcessor.RequireRecovery(options, "oneshot", false, true, true);
        Assert.Equal(style, options.Recovery.Style);
    }

    [Fact]
    public void Recovery_RejectsOffRecursionAndInteractiveRedirection()
    {
        var off = Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootRecoverProcessor.RequireRecovery(new() { Recovery = new() { Trigger = "off" } }, null, false, false, false));
        Assert.Equal("AgentDisabled", off.Code);
        Assert.Contains("agent.recovery.trigger --value manual", off.Message);
        Assert.DoesNotContain("agent.enabled", off.Message);
        Assert.Equal("RecursiveRecovery", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootRecoverProcessor.RequireRecovery(Manual(), null, true, false, false)).Code);
        Assert.Throws<UsageErrorException>(() =>
            TroubleshootRecoverProcessor.RequireRecovery(Manual(), "interactive", false, true, false));
        Assert.Throws<UsageErrorException>(() =>
            TroubleshootRecoverProcessor.RequireRecovery(Manual(), "interactive", false, false, true));
    }

    [Theory]
    [InlineData("oneshot", null, "oneshot")]
    [InlineData("interactive", null, "interactive")]
    [InlineData("oneshot", "interactive", "interactive")]
    [InlineData("interactive", "oneshot", "oneshot")]
    public async Task ManualRecovery_LinksToTheOriginalFailedCommand(
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
        var options = new AgentOptions { Provider = "github", Recovery = new() { Style = configured } };
        var captured = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            ActivityTraceId.CreateRandom().ToHexString(), ActivitySpanId.CreateRandom().ToHexString(),
            "troubleshoot recover", TenantId: "test-tenant", UploadRegion: UploadRegion.EU);

        var exit = await TroubleshootRecoverProcessor.RunAttemptAsync(
            failure, options, new Telemetry(source), captured with
            {
                CommandTraceParent = $"00-{captured.CommandTraceId}-{captured.CommandSpanId}-01",
            }, (request, context, _) =>
            {
                Assert.Equal(failure.FailedCommand, request.FailedCommand);
                Assert.Equal(failure.ErrorText, request.ErrorText);
                Assert.Equal(failure.Id, request.FailureId);
                Assert.Equal(effective, request.SessionStyle);
                Assert.Equal(RecoveryTrigger.Manual, request.Trigger);
                Assert.Equal("recover-command-profile", request.ModelProfileName);
                Assert.Equal(captured.InvocationId, context.InvocationId);
                Assert.Equal("test-tenant", context.TenantId);
                Assert.Equal(UploadRegion.EU, context.UploadRegion);
                Assert.Equal(failure.CommandTraceId, context.CommandTraceId);
                Assert.Equal(failure.CommandSpanId, context.CommandSpanId);
                var activity = Assert.IsType<Activity>(Activity.Current);
                Assert.Equal(failure.CommandTraceId, activity.TraceId.ToHexString());
                Assert.Equal(failure.CommandSpanId, activity.ParentSpanId.ToHexString());
                Assert.True(context.TryGetRecoveryParentContext(out var sessionParent));
                Assert.Equal(activity.SpanId, sessionParent.SpanId);
                Assert.Equal(effective, activity.GetTagItem(TelemetryTags.RecoverySessionStyle));
                Assert.Equal("manual", activity.GetTagItem(TelemetryTags.RecoveryTrigger));
                Assert.Equal("github", activity.GetTagItem(TelemetryTags.RecoveryProvider));
                Assert.Equal("test-tenant", activity.GetTagItem(TelemetryTags.TenantId));
                Assert.Equal(failure.CommandTraceId, activity.GetTagItem(TelemetryTags.CommandTraceId));
                return Task.FromResult(17);
            }, CancellationToken.None, styleOverride, modelProfileName: "recover-command-profile");

        Assert.Equal(17, exit);
        Assert.Equal(configured, options.Recovery.Style);
        var recovery = Assert.Single(stopped);
        Assert.Equal(ActivityStatusCode.Error, recovery.Status);
        Assert.Equal(17, recovery.GetTagItem(TelemetryTags.RecoveryExitCode));
    }

    [Fact]
    public async Task ManualRecovery_KeepsTheRecoverCommandParent_WhenTheFailureHasNoValidIds()
    {
        var failure = MakeRecord() with { CommandTraceId = "private-command", CommandSpanId = "private-error" };
        var captured = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), false);
        using var cancellation = new CancellationTokenSource();
        Activity.Current = null;
        var exit = await TroubleshootRecoverProcessor.RunAttemptAsync(
            failure, new(), new Telemetry(null), captured, (_, context, token) =>
            {
                Assert.Equal(captured, context);
                Assert.Equal(cancellation.Token, token);
                return Task.FromResult(130);
            }, cancellation.Token);
        Assert.Equal(130, exit);
    }

    [Fact]
    public async Task ManualRecovery_TimeoutsAreFailures()
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
        Activity.Current = null;
        await Assert.ThrowsAsync<TaskCanceledException>(() => TroubleshootRecoverProcessor.RunAttemptAsync(
            MakeRecord(), new(), new Telemetry(source), new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true),
            (_, _, _) => Task.FromException<int>(new TaskCanceledException("HTTP request timed out.")),
            CancellationToken.None));
        var recovery = Assert.Single(stopped);
        Assert.Equal(ActivityStatusCode.Error, recovery.Status);
        Assert.Equal(1, recovery.GetTagItem(TelemetryTags.RecoveryExitCode));
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
        var exit = await TroubleshootRecoverProcessor.RunAttemptAsync(
            MakeRecord(), options, new Telemetry(null), new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), false),
            (_, _, _) => Task.FromResult(agentExit), CancellationToken.None);
        Assert.Equal(expected, exit);
    }

    private sealed class Telemetry(ActivitySource? source) : ITelemetryService
    {
        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null)
        {
            if (source is null)
                return null;
            var previous = Activity.Current;
            Activity.Current = null;
            var activity = source.StartActivity(name ?? "CLI.Activity.Root", ActivityKind.Server);
            if (activity is null)
                Activity.Current = previous;
            return activity;
        }

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) => throw new NotSupportedException();
        public TelemetryActivityScope? StartActivityWithParent(
            string name, ActivityContext parentContext, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
            source?.StartActivity(name, kind, parentContext) is { } activity ? new TelemetryActivityScope(activity) : null;
        public TelemetryActivityScope? StartIndependentRootActivity(
            string name, ActivityKind kind = ActivityKind.Server, DateTimeOffset startTime = default, bool activate = true) =>
            StartRootActivity(startTime, name) is { } activity ? new TelemetryActivityScope(activity) : null;
        public Activity? TagInvocation(CommandInvocation commandInvocation) => throw new NotSupportedException();
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
