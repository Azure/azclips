// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Tools;
using Core.Telemetry;

namespace Core.Agent.Telemetry;

/// <summary>Drains Agent producers and cleanup before asking Core.Telemetry to finish the file.</summary>
public sealed class AgentRuntimeLifetime : IAgentProducerLifetime
{
    public static readonly TimeSpan ProducerDrainBudget = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private readonly List<Func<Task>> _cleanups = [];
    private readonly ConcurrentQueue<string> _diagnostics = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationToken _stoppingToken;
    private readonly TimeProvider _timeProvider;
    private TaskCompletionSource _changed = NewSignal();
    private TaskCompletionSource<TelemetryLayerCompletion>? _completion;
    private TelemetryLayerBudget? _drainBudget;
    private int _producers;
    private int _stoppingDisposed;
    private bool _draining;
    private bool _closing;
    private bool _closed;
    private bool _deadlineReached;

    public AgentRuntimeLifetime(ITelemetryLayerScope scope, TimeProvider? timeProvider = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _stoppingToken = _stopping.Token;
    }

    public ITelemetryLayerScope Scope { get; }
    public ITelemetryService Telemetry => Scope.Telemetry;
    public CancellationToken StoppingToken => _stoppingToken;
    public IReadOnlyList<string> Diagnostics => _diagnostics.ToArray();
    public TimeSpan RemainingDrainTime
    {
        get { lock (_gate) return _drainBudget?.Remaining ?? ProducerDrainBudget; }
    }

    public IDisposable? TryEnterProducer(bool teardown = false)
    {
        lock (_gate)
        {
            if (_closed || (_draining && (!teardown || _drainBudget!.Remaining == TimeSpan.Zero)))
                return null;
            _producers++;
            return new ProducerLease(this);
        }
    }

    public async Task CancelAsync(CancellationTokenSource source)
    {
        using var producer = TryEnterProducer(teardown: true);
        if (producer is null)
        {
            Report("cancellation_not_admitted");
            return;
        }
        await source.CancelAsync().ConfigureAwait(false);
    }

    public void BeginDrain()
    {
        IDisposable cancellation;
        lock (_gate)
        {
            if (_draining || _closed)
                return;
            _draining = true;
            _drainBudget = new TelemetryLayerBudget(_timeProvider, ProducerDrainBudget);
            _producers++;
            cancellation = new ProducerLease(this);
            PulseLocked();
        }
        _ = Task.Run(() =>
        {
            using (cancellation)
            {
                try { _stopping.Cancel(); }
                catch (Exception) { Report("cancellation_callback_failed"); }
            }
        });
    }

    public void RegisterCleanup(Func<Task> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        lock (_gate)
        {
            if (_closing)
                throw new InvalidOperationException("Agent cleanup registration has closed.");
            _cleanups.Add(cleanup);
        }
    }

    public Task<TelemetryLayerCompletion> CompleteAsync(
        Activity? root = null, IReadOnlyDictionary<string, object?>? terminalTags = null,
        ActivityStatusCode? terminalStatus = null)
    {
        Func<Task>[] cleanups;
        TaskCompletionSource<TelemetryLayerCompletion> completion;
        lock (_gate)
        {
            if (_completion is not null)
                return _completion.Task;
            _closing = true;
            _completion = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cleanups = _cleanups.ToArray();
        }
        var status = terminalStatus ?? root?.Status;
        var handoff = TryEnterProducer(teardown: true);
        BeginDrain();
        var metadataComplete = true;
        Dictionary<string, object?> frozen;
        try
        {
            frozen = terminalTags?.ToDictionary(pair => pair.Key,
                pair => (object?)JsonSerializer.SerializeToElement(pair.Value)) ?? [];
        }
        catch (Exception)
        {
            Report("terminal_metadata_failed");
            metadataComplete = false;
            frozen = [];
        }
        _ = Task.Run(() => CompleteCoreAsync(
            completion, root, frozen, status, cleanups, handoff, metadataComplete));
        return completion.Task;
    }

    public async Task<bool> RunCleanupAsync(Func<Task> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        BeginDrain();
        var producer = TryEnterProducer(teardown: true);
        if (producer is null)
        {
            MarkDeadline("cleanup_not_admitted");
            return false;
        }
        var actual = Task.Run(async () =>
        {
            using (producer)
            {
                try
                {
                    await cleanup().ConfigureAwait(false);
                    return true;
                }
                catch (Exception)
                {
                    Report("cleanup_failed");
                    return false;
                }
            }
        });
        if (!await _drainBudget!.ObserveAsync(actual).ConfigureAwait(false))
        {
            MarkDeadline("cleanup_deadline");
            return false;
        }
        return await actual.ConfigureAwait(false);
    }

    private async Task CompleteCoreAsync(
        TaskCompletionSource<TelemetryLayerCompletion> completion,
        Activity? root, IReadOnlyDictionary<string, object?> terminalTags, ActivityStatusCode? status,
        Func<Task>[] cleanups, IDisposable? handoff, bool metadataComplete)
    {
        var result = TelemetryLayerCompletion.NotPublished;
        try
        {
            var cleanupTasks = cleanups.Select(RunCleanupAsync).ToArray();
            handoff?.Dispose();
            handoff = null;
            // Each bounded waiter leaves its producer attached to the actual cleanup body.
            await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
            var drained = await DrainProducersAsync().ConfigureAwait(false);
            if (!drained)
                Report("producer_deadline");
            result = await Scope.CompleteAsync(root, terminalTags, status,
                allowPublication: drained && metadataComplete).ConfigureAwait(false);
        }
        catch (Exception)
        {
            Report("producer_completion_failed");
            try
            {
                result = await Scope.CompleteAsync(root, terminalTags, status,
                    allowPublication: false).ConfigureAwait(false);
            }
            catch (Exception) { Report("file_completion_failed"); }
        }
        finally
        {
            handoff?.Dispose();
            lock (_gate) _closed = true;
            TryDisposeStoppingSource();
            completion.TrySetResult(result);
        }
    }

    private async Task<bool> DrainProducersAsync()
    {
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_deadlineReached || _producers == 0)
                {
                    _closed = true;
                    return !_deadlineReached;
                }
                changed = _changed.Task;
            }
            if (!await _drainBudget!.ObserveAsync(changed).ConfigureAwait(false))
            {
                lock (_gate) _deadlineReached = true;
            }
        }
    }

    private void MarkDeadline(string code)
    {
        lock (_gate)
        {
            _deadlineReached = true;
            PulseLocked();
        }
        Report(code);
    }

    private void Report(string code)
    {
        _diagnostics.Enqueue(code);
        _ = Task.Run(() =>
        {
            try { Console.Error.WriteLine($"Agent telemetry: {code}"); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                Trace.TraceWarning("Agent telemetry diagnostic output is unavailable.");
            }
        });
    }

    private void TryDisposeStoppingSource()
    {
        lock (_gate)
        {
            if (!_closed || _producers != 0 || Interlocked.Exchange(ref _stoppingDisposed, 1) != 0)
                return;
        }
        _stopping.Dispose();
    }

    private void PulseLocked()
    {
        var changed = _changed;
        _changed = NewSignal();
        changed.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ProducerLease(AgentRuntimeLifetime owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            lock (owner._gate)
            {
                if (owner._draining && owner._drainBudget!.Remaining == TimeSpan.Zero)
                    owner._deadlineReached = true;
                owner._producers--;
                owner.PulseLocked();
            }
            owner.TryDisposeStoppingSource();
        }
    }
}
