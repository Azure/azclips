// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.Telemetry;
using Core.Telemetry;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public sealed class AgentProducerLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CallbackWithoutActivity_KeepsCoreRunningUntilItsBodyFinishes()
    {
        await using var harness = new AgentLayerTestHarness();
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope);
        using var root = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        var callback = lifetime.TryEnterProducer();
        Assert.NotNull(callback);

        var completion = lifetime.CompleteAsync(root?.Activity);
        Assert.Null(lifetime.TryEnterProducer());
        Assert.False(completion.IsCompleted);
        Assert.Equal(TelemetryLayerState.Running, scope.State);
        Assert.Null(scope.FileSession.PublishedPath);
        callback.Dispose();

        Assert.Equal(TelemetryLayerCompletion.Published, await completion.WaitAsync(TestTimeout));
        Assert.Single(harness.ReadEntries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProducerDeadline_PreventsPublicationAndNeverStopsTheActiveCallback(bool enabled)
    {
        var clock = new AgentTestTimeProvider();
        await using var harness = new AgentLayerTestHarness(enabled, timeProvider: clock);
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope, clock);
        using var root = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        using var callback = lifetime.TryEnterProducer();
        using var child = scope.Telemetry.StartActivityWithParent("Agent.Send", root?.Activity.Context ?? default);
        Assert.NotNull(callback);

        var completion = lifetime.CompleteAsync(root?.Activity);
        Assert.Equal(TelemetryLayerState.Running, scope.State);
        clock.Advance(AgentRuntimeLifetime.ProducerDrainBudget);
        Assert.Equal(enabled ? TelemetryLayerCompletion.NotPublished : TelemetryLayerCompletion.Disabled,
            await completion.WaitAsync(TestTimeout));
        Assert.Null(scope.FileSession.PublishedPath);
        if (enabled)
            Assert.False(child!.Activity.IsStopped);
        Assert.Null(lifetime.TryEnterProducer(teardown: true));

        child?.Dispose();
        callback.Dispose();
        Assert.Null(scope.FileSession.PublishedPath);
        Assert.Same(completion, lifetime.CompleteAsync());
    }

    [Fact]
    public async Task BusinessDuration_DoesNotConsumeTheProducerDrainBudget()
    {
        var clock = new AgentTestTimeProvider();
        await using var harness = new AgentLayerTestHarness(timeProvider: clock);
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope, clock);
        using var root = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        using var callback = lifetime.TryEnterProducer();
        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(AgentRuntimeLifetime.ProducerDrainBudget, lifetime.RemainingDrainTime);

        var completion = lifetime.CompleteAsync(root?.Activity);
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(TimeSpan.FromSeconds(1), lifetime.RemainingDrainTime);
        Assert.False(completion.IsCompleted);
        Assert.Equal(TelemetryLayerState.Running, scope.State);
        callback!.Dispose();
        Assert.Equal(TelemetryLayerCompletion.Published, await completion.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task DisabledLifetime_BoundsSynchronousCleanupWithoutPublishingFiles()
    {
        var clock = new AgentTestTimeProvider();
        await using var harness = new AgentLayerTestHarness(false, timeProvider: clock);
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope, clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var cleanup = lifetime.RunCleanupAsync(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(TestTimeout))
                throw new TimeoutException("Cleanup barrier was not released.");
            exited.TrySetResult();
            return Task.CompletedTask;
        });
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            var completion = lifetime.CompleteAsync();
            clock.Advance(AgentRuntimeLifetime.ProducerDrainBudget);
            Assert.False(await cleanup.WaitAsync(TestTimeout));
            Assert.Equal(TelemetryLayerCompletion.Disabled, await completion.WaitAsync(TestTimeout));
            Assert.Null(scope.FileSession.PublishedPath);
            Assert.False(Directory.Exists(harness.DirectoryPath));
        }
        finally
        {
            release.Set();
            await exited.Task.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task StopCallbacks_UseTheOriginalDeadlineBeforeCoreCompletion()
    {
        var clock = new AgentTestTimeProvider();
        await using var harness = new AgentLayerTestHarness(timeProvider: clock);
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope, clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var registration = lifetime.StoppingToken.Register(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(TestTimeout))
                throw new TimeoutException("Cancellation barrier was not released.");
            exited.TrySetResult();
        });
        lifetime.BeginDrain();
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            clock.Advance(TimeSpan.FromSeconds(9));
            Assert.Equal(TimeSpan.FromSeconds(1), lifetime.RemainingDrainTime);
            var completion = lifetime.CompleteAsync();
            Assert.Equal(TelemetryLayerState.Running, scope.State);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
            Assert.Contains("producer_deadline", lifetime.Diagnostics);
        }
        finally
        {
            release.Set();
            await exited.Task.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task CancellationDuringDrain_IsTrackedAndClosedCancellationIsRejected()
    {
        await using var harness = new AgentLayerTestHarness();
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope);
        using var root = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        using var cancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(TestTimeout))
                throw new TimeoutException("Cancellation was not released.");
        });
        lifetime.BeginDrain();
        var cancelling = lifetime.CancelAsync(cancellation);
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            var completion = lifetime.CompleteAsync(root?.Activity);
            Assert.False(completion.IsCompleted);
            Assert.Equal(TelemetryLayerState.Running, scope.State);
            release.Set();
            await cancelling.WaitAsync(TestTimeout);
            Assert.Equal(TelemetryLayerCompletion.Published, await completion.WaitAsync(TestTimeout));
        }
        finally
        {
            release.Set();
            await cancelling.WaitAsync(TestTimeout);
        }

        using var late = new CancellationTokenSource();
        await lifetime.CancelAsync(late);
        Assert.False(late.IsCancellationRequested);
        Assert.Contains("cancellation_not_admitted", lifetime.Diagnostics);
    }

    [Fact]
    public async Task CleanupFailure_StillRecordsTeardownAndPreservesTheFrozenBusinessResult()
    {
        await using var harness = new AgentLayerTestHarness();
        var scope = harness.CreateScope();
        var lifetime = new AgentRuntimeLifetime(scope);
        using var root = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        Assert.NotNull(root);
        root.Activity.SetStatus(ActivityStatusCode.Error);
        var terminal = new Dictionary<string, object?> { [TelemetryTags.RecoveryOutcome] = "error" };
        lifetime.RegisterCleanup(() =>
        {
            Assert.Equal(TelemetryLayerState.Running, scope.State);
            Assert.False(root.Activity.IsStopped);
            using var producer = lifetime.TryEnterProducer(teardown: true);
            Assert.NotNull(producer);
            using var child = scope.Telemetry.StartActivityWithParent("Agent.Send", root.Activity.Context);
            Assert.NotNull(child);
            root.Activity.SetStatus(ActivityStatusCode.Ok);
            root.Activity.SetTag(TelemetryTags.RecoveryOutcome, "wrong");
            terminal[TelemetryTags.RecoveryOutcome] = "changed after freeze";
            throw new InvalidOperationException("Sensitive details must not appear in diagnostics.");
        });

        Assert.Equal(TelemetryLayerCompletion.Published,
            await lifetime.CompleteAsync(root.Activity, terminal).WaitAsync(TestTimeout));
        Assert.Contains("cleanup_failed", lifetime.Diagnostics);
        Assert.DoesNotContain(lifetime.Diagnostics, code => code.Contains("Sensitive", StringComparison.Ordinal));
        Assert.False(scope.FileSession.Failed);
        var record = Assert.Single(harness.ReadEntries(),
            entry => entry.GetProperty("operationName").GetString() == "Agent.Session");
        Assert.Equal("Error", record.GetProperty("status").GetString());
        Assert.Equal("error", AgentLayerTestHarness.Tags(record)[TelemetryTags.RecoveryOutcome].GetString());
    }
}
