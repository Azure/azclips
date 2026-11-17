// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Time.Testing;

namespace Tests.Common.Utilities;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that reports each timer it arms, so a test can wait until the
/// code under test has started a wait before it advances the clock.
/// </summary>
public sealed class SignalingFakeTimeProvider : FakeTimeProvider
{
    private readonly object _gate = new();
    private readonly List<TimeSpan> _armed = [];
    private TaskCompletionSource _changed = NewSignal();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new SignalingTimer(this, base.CreateTimer(callback, state, dueTime, period));
        NoteArmed(dueTime);
        return timer;
    }

    /// <summary>
    /// Waits until a timer is created or changed with a due time that <paramref name="match"/> accepts,
    /// and returns that due time. Each armed timer satisfies one wait.
    /// </summary>
    public async Task<TimeSpan> WaitForTimerAsync(Func<TimeSpan, bool> match, TimeSpan timeout)
    {
        using var expiry = new CancellationTokenSource(timeout);
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                var index = _armed.FindIndex(dueTime => match(dueTime));
                if (index >= 0)
                {
                    var dueTime = _armed[index];
                    _armed.RemoveAt(index);
                    return dueTime;
                }
                changed = _changed.Task;
            }
            try
            {
                await changed.WaitAsync(expiry.Token);
            }
            catch (OperationCanceledException) when (expiry.IsCancellationRequested)
            {
                throw new TimeoutException($"No matching timer was armed within {timeout}.");
            }
        }
    }

    /// <summary>Waits until a timer is armed with exactly <paramref name="dueTime"/>.</summary>
    public Task WaitForTimerAsync(TimeSpan dueTime, TimeSpan timeout) =>
        WaitForTimerAsync(armed => armed == dueTime, timeout);

    private void NoteArmed(TimeSpan dueTime)
    {
        if (dueTime == Timeout.InfiniteTimeSpan)
            return;
        TaskCompletionSource changed;
        lock (_gate)
        {
            _armed.Add(dueTime);
            changed = _changed;
            _changed = NewSignal();
        }
        changed.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class SignalingTimer(SignalingFakeTimeProvider owner, ITimer timer) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            var changed = timer.Change(dueTime, period);
            if (changed)
                owner.NoteArmed(dueTime);
            return changed;
        }

        public void Dispose() => timer.Dispose();

        public ValueTask DisposeAsync() => timer.DisposeAsync();
    }
}
