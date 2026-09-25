// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.AgentMode;
using Core.Telemetry;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public class AgentLayerAsyncWriterTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(130, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(130, false)]
    public async Task RunSessionAsync_ChildDisposeCannotBlockAdmissionToBoundedCompletion(
        int expectedExit, bool expireBudget)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestArtifacts",
            "AgentLayerAsyncWriter", Guid.NewGuid().ToString("N"));
        using var blockedDirectory = new BlockingDirectory(directory);
        var clock = new FileBudgetTimeProvider();
        var factory = new TelemetryLayerScopeFactory(
            blockedDirectory, new AgentTestConfiguration(), timeProvider: clock);
        var scope = factory.CreateScope("Agent", new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true));
        Assert.True(scope.TelemetryProvider.ActivitySource.HasListeners());
        var businessReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposedServices = 0;
        var publishedAfterRelease = false;
        var host = Task.Run(() => AgentModeEntry.RunSessionAsync(() => scope, (session, _) =>
        {
            using var producer = scope.TryEnterProducer();
            try
            {
                using var child = scope.Telemetry.StartActivityWithParent("Agent.Send", session!.Context);
                return expectedExit switch
                {
                    1 => throw new InvalidOperationException("Expected offline business failure"),
                    130 => throw new OperationCanceledException(),
                    _ => Task.CompletedTask,
                };
            }
            finally
            {
                businessReturned.TrySetResult();
            }
        }, () => Interlocked.Increment(ref disposedServices)));

        try
        {
            await blockedDirectory.Entered.Task.WaitAsync(TestTimeout);
            await Task.WhenAny(businessReturned.Task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.True(businessReturned.Task.IsCompleted,
                $"Child.Dispose prevented the actual Agent host from entering completion; state={scope.State}.");
            await clock.FileWaitArmed.Task.WaitAsync(TestTimeout);
            Assert.Equal(TelemetryLayerState.Fenced, scope.State);
            Assert.False(scope.FileSession.HasRecords);
            Assert.True(scope.TelemetryProvider.ActivitySource.HasListeners());
            clock.Advance(expireBudget ? LayerTelemetryScope.FileCompletionBudget : TimeSpan.FromSeconds(1));
            if (!expireBudget)
            {
                Assert.Equal(TelemetryLayerState.Fenced, scope.State);
                Assert.False(host.IsCompleted);
                blockedDirectory.Release();
            }

            Assert.Equal(expectedExit, await host.WaitAsync(TestTimeout));
            Assert.Equal(1, disposedServices);
            // Query only after the real host returned; this does not start completion from a side caller.
            Assert.Equal(expireBudget ? TelemetryLayerCompletion.NotPublished : TelemetryLayerCompletion.Published,
                await scope.CompleteAsync());
            Assert.Equal(!expireBudget, scope.FileSession.Published);
            Assert.Equal(!expireBudget, File.Exists(scope.FileSession.PublishedPath));
            Assert.Equal(expireBudget, scope.TelemetryProvider.ActivitySource.HasListeners());
            if (!expireBudget)
            {
                var entries = AgentLayerTestHarness.ReadEntries(scope);
                Assert.Equal(["Agent.Send", "Agent.Session"],
                    entries.Select(entry => entry.GetProperty("OperationName").GetString()));
            }
        }
        finally
        {
            blockedDirectory.Release();
            try
            {
                await host.WaitAsync(TestTimeout);
                await WaitForProviderReleaseAsync(scope).WaitAsync(TestTimeout);
                await scope.DisposeAsync();
                publishedAfterRelease = scope.FileSession.Published || File.Exists(scope.FileSession.PublishedPath);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }
        Assert.Equal(!expireBudget, publishedAfterRelease);
    }

    private static async Task WaitForProviderReleaseAsync(LayerTelemetryScope scope)
    {
        while (scope.TelemetryProvider.ActivitySource.HasListeners())
            await Task.Delay(1);
    }

    private sealed class BlockingDirectory(string path) : ITraceDirectoryProvider, IDisposable
    {
        private readonly ManualResetEventSlim _released = new();
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string GetTraceDirectory()
        {
            Entered.TrySetResult();
            if (!_released.Wait(TestTimeout))
                throw new TimeoutException("The controlled directory seam was not released.");
            return path;
        }

        internal void Release() => _released.Set();
        public void Dispose() => Release();
    }

    private sealed class FileBudgetTimeProvider : TimeProvider
    {
        private readonly AgentTestTimeProvider _clock = new();
        internal TaskCompletionSource FileWaitArmed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override long TimestampFrequency => _clock.TimestampFrequency;
        public override long GetTimestamp() => _clock.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = _clock.CreateTimer(callback, state, dueTime, period);
            NoteDueTime(dueTime);
            return new ObservedTimer(this, timer);
        }

        private void NoteDueTime(TimeSpan dueTime)
        {
            if (dueTime > TimeSpan.Zero && dueTime <= LayerTelemetryScope.FileCompletionBudget)
                FileWaitArmed.TrySetResult();
        }

        internal void Advance(TimeSpan amount) => _clock.Advance(amount);

        private sealed class ObservedTimer(FileBudgetTimeProvider clock, ITimer timer) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                var changed = timer.Change(dueTime, period);
                clock.NoteDueTime(dueTime);
                return changed;
            }

            public void Dispose() => timer.Dispose();
            public ValueTask DisposeAsync() => timer.DisposeAsync();
        }
    }
}
