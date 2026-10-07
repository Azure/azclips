// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

public enum TelemetryLayerState
{
    Running,
    Draining,
    Fenced,
    Sealed,
    /// <summary>An admitted rename has not returned a result within the completion budget.</summary>
    PublicationUnconfirmed,
    NotPublished,
    Published,
}

public sealed class TelemetryLayerBudget(TimeProvider timeProvider, TimeSpan duration)
{
    internal static readonly TimeSpan SchedulingTolerance = TimeSpan.FromMilliseconds(1);
    private readonly long _started = timeProvider.GetTimestamp();

    public TimeSpan Remaining
    {
        get
        {
            var remaining = duration - timeProvider.GetElapsedTime(_started);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public async Task<bool> ObserveAsync(Task task)
    {
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return true;
        }
        if (Remaining == TimeSpan.Zero)
            return false;
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Register unarmed: any registration time consumes the original budget,
        // rather than becoming extra time on an already captured relative delay.
        using var timer = timeProvider.CreateTimer(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            expired, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        while (!task.IsCompleted)
        {
            var remaining = Remaining;
            if (remaining == TimeSpan.Zero)
                return false;
            if (!timer.Change(remaining, Timeout.InfiniteTimeSpan))
                throw new InvalidOperationException("The telemetry deadline timer could not be armed.");
            var afterActivation = Remaining;
            if (afterActivation == TimeSpan.Zero && !task.IsCompleted)
                return false;

            // Each correction consumes more than one millisecond of this same budget.
            // The tolerance avoids busy-rearming for normal sub-millisecond call overhead.
            if (remaining - afterActivation > SchedulingTolerance)
                continue;
            if (await Task.WhenAny(task, expired.Task).ConfigureAwait(false) != task)
                return false;
            break;
        }
        await task.ConfigureAwait(false);
        return true;
    }
}

internal enum TelemetryLayerCheckpoint
{
    CreationReserved,
    CaptureReserved,
    WriteAccepted,
    BeforeProviderDispose,
    BeforeRename,
}

internal sealed class TelemetryLayerScopeOptions
{
    internal TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    internal ILayerTraceFileSystem FileSystem { get; init; } = new LayerTraceFileSystem();
    internal Func<Sampler> SamplerFactory { get; init; } =
        () => new ParentBasedSampler(new AlwaysOnSampler());
    internal IReadOnlyList<Func<BaseProcessor<Activity>>> ProcessorFactories { get; init; } = [];
    internal Action<TelemetryLayerCheckpoint>? Checkpoint { get; init; }
    internal Action<string>? Diagnostic { get; init; }
}

internal sealed class LayerTelemetryDiagnostics(Action<string>? sink)
{
    private readonly ConcurrentQueue<string> _codes = new();

    internal IReadOnlyList<string> Codes => _codes.ToArray();

    internal void Report(string code)
    {
        _codes.Enqueue(code);
        _ = Task.Run(() =>
        {
            using var suppression = SuppressInstrumentationScope.Begin();
            try
            {
                if (sink is not null)
                    sink(code);
                else
                    Console.Error.WriteLine($"Layer telemetry: {code}.");
            }
            catch
            {
                // Diagnostics must neither recurse nor delay or replace a business failure.
            }
        });
    }
}

internal sealed class LayerTelemetryProvider(string sourceName) : ITelemetryProvider
{
    private TracerProvider? _ownedProvider;
    private int _disposed;

    public ActivitySource ActivitySource { get; } = new(sourceName);
    public TracerProvider TracerProvider => _ownedProvider ?? TracerProvider.Default;

    internal void SetProvider(TracerProvider provider) => _ownedProvider = provider;

    internal void DisposeOwned()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        // A disabled scope has no owned provider; never dispose TracerProvider.Default.
        _ownedProvider?.Dispose();
        ActivitySource.Dispose();
    }
}

internal sealed class LayerFileTelemetrySender(LayerTelemetryScope scope) : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder) =>
        builder.AddProcessor(new LayerTelemetryProcessor(scope));

    private sealed class LayerTelemetryProcessor(LayerTelemetryScope owner) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => owner.OnActivityEnded(data);
    }
}

internal sealed class LayerTelemetrySampler(LayerTelemetryScope scope, Sampler inner) : Sampler
{
    internal const string CreationTag = "azclips.layer.creation";

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var stamp = scope.CreationStamp;
        if (stamp is null)
            return new SamplingResult(SamplingDecision.Drop);
        var result = inner.ShouldSample(in samplingParameters);
        if (result.Decision != SamplingDecision.RecordAndSample)
            return result;
        // A listener can start another activity reentrantly. Bind this decision to
        // the sampled activity itself, not a mutable "last decision" on the lease.
        var attributes = (result.Attributes ?? [])
            .Where(attribute => attribute.Key != CreationTag)
            .Append(new KeyValuePair<string, object>(CreationTag, stamp));
        return new SamplingResult(result.Decision, attributes, result.TraceStateString);
    }
}
