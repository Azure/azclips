// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryLayerBudgetTests
{
    [Fact]
    public async Task DeadlineDuringTimerRegistration_DoesNotStartAnotherFullWait()
    {
        var clock = new RegistrationAdvancingTimeProvider(TimeSpan.FromSeconds(10));
        var budget = new TelemetryLayerBudget(clock, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observation = budget.ObserveAsync(pending.Task);
        try
        {
            Assert.Equal(TimeSpan.Zero, budget.Remaining);
            var finished = await Task.WhenAny(observation, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(observation, finished);
            Assert.False(await observation);
            Assert.False(pending.Task.IsCompleted);
        }
        finally
        {
            pending.TrySetResult();
            await observation;
        }
    }

    [Theory]
    [InlineData(10000, 9000)]
    [InlineData(10000, 3000)]
    [InlineData(2000, 1500)]
    public Task PartialTimerRegistration_StillExpiresAtTheOriginalDeadline(
        int budgetMilliseconds, int registrationMilliseconds) =>
        AssertOriginalDeadlineAsync(budgetMilliseconds, registrationMilliseconds, duringActivation: false);

    [Theory]
    [InlineData(10000, 9000)]
    [InlineData(10000, 3000)]
    [InlineData(2000, 1500)]
    public Task PartialTimerActivation_StillExpiresAtTheOriginalDeadline(
        int budgetMilliseconds, int activationMilliseconds) =>
        AssertOriginalDeadlineAsync(budgetMilliseconds, activationMilliseconds, duringActivation: true);

    private static async Task AssertOriginalDeadlineAsync(
        int budgetMilliseconds, int delayMilliseconds, bool duringActivation)
    {
        var duration = TimeSpan.FromMilliseconds(budgetMilliseconds);
        var setupDelay = TimeSpan.FromMilliseconds(delayMilliseconds);
        var clock = new RegistrationAdvancingTimeProvider(
            duringActivation ? TimeSpan.Zero : setupDelay,
            activationDelay: duringActivation ? setupDelay : TimeSpan.Zero);
        var deadline = clock.GetTimestamp() + duration.Ticks;
        var budget = new TelemetryLayerBudget(clock, duration);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observation = budget.ObserveAsync(pending.Task);
        try
        {
            Assert.Equal(duration - setupDelay, budget.Remaining);
            Assert.False(observation.IsCompleted);
            clock.Advance(duration - setupDelay);
            Assert.Equal(deadline, clock.GetTimestamp());
            Assert.Equal(TimeSpan.Zero, budget.Remaining);
            Assert.Equal(deadline, clock.LastScheduledDeadline);
            var finished = await Task.WhenAny(observation, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(observation, finished);
            Assert.False(await observation);
            Assert.False(pending.Task.IsCompleted);
            Assert.Equal(1, clock.DisposalCount);
        }
        finally
        {
            pending.TrySetResult();
            await observation;
        }
    }

    [Fact]
    public async Task DeadlineDuringActivation_DoesNotWaitForItsLateTimer()
    {
        var clock = new RegistrationAdvancingTimeProvider(
            TimeSpan.Zero, activationDelay: TimeSpan.FromSeconds(10));
        var budget = new TelemetryLayerBudget(clock, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.False(await budget.ObserveAsync(pending.Task));
        Assert.Equal(TimeSpan.Zero, budget.Remaining);
        Assert.False(pending.Task.IsCompleted);
        Assert.Equal(1, clock.DisposalCount);
    }

    [Fact]
    public async Task WorkCompletingDuringActivation_WinsAndDisposesTheTimer()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new RegistrationAdvancingTimeProvider(
            TimeSpan.Zero, activationDelay: TimeSpan.FromSeconds(9),
            onActivation: () => pending.TrySetResult());
        var budget = new TelemetryLayerBudget(clock, TimeSpan.FromSeconds(10));

        Assert.True(await budget.ObserveAsync(pending.Task));
        Assert.Equal(1, clock.ActivationCount);
        Assert.Equal(1, clock.DisposalCount);
    }

    [Fact]
    public async Task CompletedWork_DoesNotCreateATimer()
    {
        var clock = new RegistrationAdvancingTimeProvider(TimeSpan.Zero);
        var budget = new TelemetryLayerBudget(clock, TimeSpan.FromSeconds(10));

        Assert.True(await budget.ObserveAsync(Task.CompletedTask));
        Assert.Equal(0, clock.CreationCount);
    }

    [Fact]
    public async Task RepeatedSlowActivations_ConsumeTheSameFiniteBudget()
    {
        var clock = new RegistrationAdvancingTimeProvider(
            TimeSpan.Zero, activationDelay: TimeSpan.FromSeconds(3), repeatActivationDelay: true);
        var budget = new TelemetryLayerBudget(clock, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.False(await budget.ObserveAsync(pending.Task));
        Assert.Equal(4, clock.ActivationCount);
        Assert.Equal(TimeSpan.Zero, budget.Remaining);
        Assert.False(pending.Task.IsCompleted);
        Assert.Equal(1, clock.DisposalCount);
    }

    [Fact]
    public async Task SubMillisecondActivationSkew_DoesNotBusyRearm()
    {
        var duration = TimeSpan.FromMilliseconds(10);
        var setupDelay = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2);
        var clock = new RegistrationAdvancingTimeProvider(TimeSpan.Zero, activationDelay: setupDelay);
        var budget = new TelemetryLayerBudget(clock, duration);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observation = budget.ObserveAsync(pending.Task);
        try
        {
            Assert.Equal(1, clock.ActivationCount);
            Assert.Equal(duration.Ticks + setupDelay.Ticks, clock.LastScheduledDeadline);
            Assert.InRange(clock.LastScheduledDeadline!.Value,
                duration.Ticks, duration.Ticks + TelemetryLayerBudget.SchedulingTolerance.Ticks);
            clock.Advance(duration);
            Assert.False(await observation.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(1, clock.DisposalCount);
        }
        finally
        {
            pending.TrySetResult();
            await observation;
        }
    }

    [Fact]
    public async Task RejectedActivation_FailsExplicitlyAndDisposesTheTimer()
    {
        var clock = new RegistrationAdvancingTimeProvider(TimeSpan.Zero, rejectActivation: true);
        var budget = new TelemetryLayerBudget(clock, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => budget.ObserveAsync(pending.Task));

        Assert.Contains("deadline timer", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, clock.ActivationCount);
        Assert.Equal(1, clock.DisposalCount);
    }

    private sealed class RegistrationAdvancingTimeProvider(
        TimeSpan registrationDelay,
        TimeSpan activationDelay = default,
        Action? onActivation = null,
        bool repeatActivationDelay = false,
        bool rejectActivation = false) : TimeProvider
    {
        private readonly TelemetryLayerManualTimeProvider _clock = new();
        private bool _firstTimer = true;

        internal long? LastScheduledDeadline { get; private set; }
        internal int CreationCount { get; private set; }
        internal int ActivationCount { get; private set; }
        internal int DisposalCount { get; private set; }
        private bool RejectActivation => rejectActivation;
        public override long TimestampFrequency => _clock.TimestampFrequency;
        public override long GetTimestamp() => _clock.GetTimestamp();

        public override ITimer CreateTimer(
            TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            CreationCount++;
            if (_firstTimer)
            {
                _firstTimer = false;
                _clock.Advance(registrationDelay);
            }
            var timer = _clock.CreateTimer(callback, state, dueTime, period);
            NoteDeadline(dueTime);
            return new TrackedTimer(this, timer);
        }

        internal void Advance(TimeSpan duration) => _clock.Advance(duration);

        private void BeforeActivation()
        {
            if (++ActivationCount == 1 || repeatActivationDelay)
            {
                _clock.Advance(activationDelay);
                onActivation?.Invoke();
            }
        }

        private void NoteDeadline(TimeSpan dueTime)
        {
            if (dueTime != Timeout.InfiniteTimeSpan)
                LastScheduledDeadline = _clock.GetTimestamp() + dueTime.Ticks;
        }

        private sealed class TrackedTimer(RegistrationAdvancingTimeProvider clock, ITimer timer) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (dueTime != Timeout.InfiniteTimeSpan)
                    clock.BeforeActivation();
                if (clock.RejectActivation)
                    return false;
                var changed = timer.Change(dueTime, period);
                clock.NoteDeadline(dueTime);
                return changed;
            }

            public void Dispose()
            {
                clock.DisposalCount++;
                timer.Dispose();
            }
            public ValueTask DisposeAsync()
            {
                Dispose();
                return default;
            }
        }
    }
}
