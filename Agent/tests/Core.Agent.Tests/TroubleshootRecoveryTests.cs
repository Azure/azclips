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
    public void Analysis_UsesTheNewTriggerAndSharedStyle(string trigger, string style)
    {
        var options = new AgentOptions { Recovery = new() { Trigger = trigger, Style = style } };
        TroubleshootAnalyzeProcessor.RequireAnalysis(options, null, false, false, false);
        TroubleshootAnalyzeProcessor.RequireAnalysis(options, "oneshot", false, true, true);
        Assert.Equal(style, options.Recovery.Style);
    }

    [Fact]
    public void Analysis_RejectsOffRecursionAndInteractiveRedirection()
    {
        var off = Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootAnalyzeProcessor.RequireAnalysis(new() { Recovery = new() { Trigger = "off" } }, null, false, false, false));
        Assert.Equal("AgentDisabled", off.Code);
        Assert.Contains("agent.recovery --value manual", off.Message);
        Assert.DoesNotContain("agent.enabled", off.Message);
        Assert.Equal("RecursiveRecovery", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootAnalyzeProcessor.RequireAnalysis(Manual(), null, true, false, false)).Code);
        Assert.Throws<UsageErrorException>(() =>
            TroubleshootAnalyzeProcessor.RequireAnalysis(Manual(), "interactive", false, true, false));
        Assert.Throws<UsageErrorException>(() =>
            TroubleshootAnalyzeProcessor.RequireAnalysis(Manual(), "interactive", false, false, true));
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
        var options = new AgentOptions { Provider = "github", Recovery = new() { Style = configured } };

        using var command = source.StartActivity("command", ActivityKind.Server)!;
        var exit = await TroubleshootAnalyzeProcessor.RunAttemptAsync(
            failure, options, new Telemetry(source), ("test-tenant", "EU"), (request, traceparent, _) =>
            {
                Assert.Equal(failure.FailedCommand, request.FailedCommand);
                Assert.Equal(failure.ErrorText, request.ErrorText);
                Assert.Equal(failure.Id, request.FailureId);
                Assert.Equal(failure.CommandTraceId, request.CommandTraceId);
                Assert.Equal(effective, request.Style);
                Assert.Equal(RecoveryTrigger.Manual, request.Trigger);
                Assert.Equal(command.TraceId.ToHexString(), request.RequestTraceId);
                Assert.Equal(command.SpanId.ToHexString(), request.RequestSpanId);
                Assert.Equal("test-tenant", request.TenantId);
                Assert.Equal("EU", request.DataBoundary);
                var activity = Assert.IsType<Activity>(Activity.Current);
                Assert.Equal(default, activity.ParentSpanId);
                Assert.NotEqual(failure.CommandTraceId, activity.TraceId.ToHexString());
                Assert.NotEqual(request.RequestTraceId, activity.TraceId.ToHexString());
                Assert.Equal(activity.Id, traceparent);
                Assert.Equal(effective, activity.GetTagItem(TelemetryTags.RecoveryStyle));
                Assert.Equal("manual", activity.GetTagItem(TelemetryTags.RecoveryTrigger));
                Assert.Equal("github", activity.GetTagItem(TelemetryTags.RecoveryProvider));
                return Task.FromResult(17);
            }, CancellationToken.None, styleOverride);

        Assert.Equal(17, exit);
        Assert.Equal(configured, options.Recovery.Style);
        var recovery = Assert.Single(stopped);
        Assert.Equal(ActivityStatusCode.Error, recovery.Status);
        Assert.Equal(17, recovery.GetTagItem(TelemetryTags.RecoveryExitCode));
    }

    [Fact]
    public async Task ManualRecovery_OptOutDoesNotInventTraceIdsOrReplaceFailures()
    {
        var failure = MakeRecord() with { CommandTraceId = "private-command", CommandSpanId = "private-error" };
        using var cancellation = new CancellationTokenSource();
        Activity.Current = null;
        var exit = await TroubleshootAnalyzeProcessor.RunAttemptAsync(
            failure, new(), new Telemetry(null), (null, null), (request, traceparent, token) =>
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
        Activity.Current = null;
        await Assert.ThrowsAsync<TaskCanceledException>(() => TroubleshootAnalyzeProcessor.RunAttemptAsync(
            MakeRecord(), new(), new Telemetry(source), (null, null),
            (_, _, _) => Task.FromException<int>(new TaskCanceledException("HTTP request timed out.")),
            new CancellationToken(cancelled)));
        var recovery = Assert.Single(stopped);
        Assert.Equal(expectedStatus, recovery.Status);
        Assert.Equal(expectedExit, recovery.GetTagItem(TelemetryTags.RecoveryExitCode));
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
        var exit = await TroubleshootAnalyzeProcessor.RunAttemptAsync(
            MakeRecord(), options, new Telemetry(null), (null, null),
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
        public Activity? TagInvocation(CommandInvocation commandInvocation) => throw new NotSupportedException();
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
