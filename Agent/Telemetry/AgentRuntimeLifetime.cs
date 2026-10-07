// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;

namespace Core.Agent.Telemetry;

/// <summary>Owns resources from before SDK creation until the layer's bounded completion.</summary>
public sealed class AgentRuntimeLifetime(ITelemetryLayerScope scope)
{
    private readonly object _gate = new();
    private readonly List<Func<Task>> _cleanups = [];
    private Task<TelemetryLayerCompletion>? _completion;
    private bool _closing;

    public ITelemetryLayerScope Scope { get; } = scope;
    public ITelemetryService Telemetry => Scope.Telemetry;

    public async Task CancelAsync(CancellationTokenSource source)
    {
        using var producer = Scope.TryEnterProducer();
        await source.CancelAsync().ConfigureAwait(false);
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
        Activity? root, IReadOnlyDictionary<string, object?> terminalTags, ActivityStatusCode? status)
    {
        lock (_gate)
        {
            if (_completion is not null)
                return _completion;
            _closing = true;
            return _completion = CompleteCoreAsync(root, terminalTags, status, _cleanups.ToArray());
        }
    }

    private async Task<TelemetryLayerCompletion> CompleteCoreAsync(
        Activity? root, IReadOnlyDictionary<string, object?> terminalTags, ActivityStatusCode? status,
        Func<Task>[] cleanups)
    {
        // Keep admission alive across the handoff: Complete freezes the business result before
        // any SDK/cache cleanup starts, but must not seal an otherwise empty scope in between.
        var handoff = Scope.TryEnterProducer();
        var completion = Scope.CompleteAsync(root, terminalTags, status);
        Task<bool>[] cleanupTasks;
        try
        {
            cleanupTasks = cleanups.Select(Scope.RunCleanupAsync).ToArray();
        }
        finally
        {
            handoff?.Dispose();
        }

        // Each cleanup retains its own real producer, even when its bounded waiter times out.
        // Cache cancellation must not sit behind an SDK dispose that never returns.
        await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
        return await completion.ConfigureAwait(false);
    }
}
