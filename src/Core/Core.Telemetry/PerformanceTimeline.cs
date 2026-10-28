// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Records monotonic phase measurements independently of telemetry. Startup phases can
/// be replayed as spans, and live spans share their recorded duration with local reports.
/// </summary>
public sealed class PerformanceTimeline
{
    private readonly string _enclosingSpanName;
    private readonly TimeProvider _clock;
    private readonly long _origin;
    private readonly object _lock = new();
    private readonly List<Phase> _roots = new();
    private readonly AsyncLocal<Phase?> _current = new();
    private bool _replayed;

    /// <param name="enclosingSpanName">
    /// Name of the span that encloses every recorded phase. By convention
    /// <c>"{ClientType}.Performance"</c> — <c>"CLI.Performance"</c>,
    /// <c>"PowerShell.Performance"</c>. Deliberately has no default: a shared default
    /// would silently mislabel whichever client forgot to override it.
    /// </param>
    public PerformanceTimeline(string enclosingSpanName)
        : this(enclosingSpanName, TimeProvider.System)
    {
    }

    internal PerformanceTimeline(string enclosingSpanName, TimeProvider clock)
    {
        _enclosingSpanName = enclosingSpanName;
        _clock = clock;
        _origin = clock.GetTimestamp();
        StartedAt = clock.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// When the timeline began. Used to backdate the root activity so the replayed
    /// phases nest beneath it rather than starting before it.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Times the enclosing block. Nested calls produce nested spans; sequential calls
    /// produce siblings.
    /// </summary>
    public IDisposable Measure(string name) => Begin(name, replay: true);

    /// <summary>Records a local phase without adding a telemetry span.</summary>
    public IDisposable MeasureLocal(string name) => Begin(name, replay: false);

    /// <summary>Records a phase and its live telemetry span from the same clock.</summary>
    public IDisposable MeasureActivity(string name, ITelemetryService telemetry)
    {
        var phase = Begin(name, replay: false);
        phase.Activity = telemetry.StartActivity(name, ActivityKind.Internal, StartedAt + phase.Start);
        return phase;
    }

    private TimeSpan Elapsed => _clock.GetElapsedTime(_origin, _clock.GetTimestamp());

    private Phase Begin(string name, bool replay)
    {
        lock (_lock)
        {
            var parent = _current.Value;
            while (parent is { IsClosed: true })
            {
                parent = parent.Parent;
            }

            var phase = new Phase(name, this, parent, Elapsed, replay);
            (parent?.Children ?? _roots).Add(phase);
            _current.Value = phase;
            return phase;
        }
    }

    /// <summary>
    /// Replays the startup phases once beneath the current activity. Call once the
    /// root activity has started. No-op when nothing was measured, and safe when
    /// telemetry is disabled — every <c>StartActivity</c> then returns null.
    /// </summary>
    public void Replay(ITelemetryService telemetry)
    {
        List<RecordedPhase> roots;
        lock (_lock)
        {
            if (_replayed || _roots.Count == 0) return;
            _replayed = true;
            var elapsed = Elapsed;
            roots = _roots.Select(p => p.Capture(elapsed)).ToList();
        }

        using var enclosing = telemetry.StartActivity(_enclosingSpanName, ActivityKind.Internal, StartedAt);
        foreach (var root in roots)
        {
            Emit(telemetry, root);
        }

        // End the enclosing span at the latest end across all top-level phases.
        enclosing?.SetEndTime(StartedAt + roots.Max(p => p.End));
    }

    private void Emit(ITelemetryService telemetry, RecordedPhase phase)
    {
        using var span = phase.Replay
            ? telemetry.StartActivity(phase.Name, ActivityKind.Internal, StartedAt + phase.Start)
            : null;
        foreach (var child in phase.Children)
        {
            Emit(telemetry, child);
        }

        span?.SetEndTime(StartedAt + phase.End);
    }

    /// <summary>Captures immutable offsets and coverage without requiring telemetry.</summary>
    public Snapshot Capture()
    {
        lock (_lock)
        {
            var elapsed = Elapsed;
            var measurements = new List<Measurement>();
            foreach (var root in _roots)
            {
                Flatten(root, 0);
            }

            var covered = TimeSpan.Zero;
            var end = TimeSpan.Zero;
            foreach (var row in measurements.OrderBy(p => p.Start))
            {
                if (row.End > end)
                {
                    covered += row.End - (row.Start > end ? row.Start : end);
                    end = row.End;
                }
            }
            return new Snapshot(elapsed, elapsed - covered, measurements.AsReadOnly());

            void Flatten(Phase phase, int depth)
            {
                measurements.Add(new Measurement(phase.Name, phase.Start, phase.End ?? elapsed, depth, phase.IsClosed));
                foreach (var child in phase.Children)
                {
                    Flatten(child, depth + 1);
                }
            }
        }
    }

    public sealed record Measurement(string Name, TimeSpan Start, TimeSpan End, int Depth, bool IsComplete)
    {
        public TimeSpan Duration => End - Start;
    }

    public sealed record Snapshot(TimeSpan Elapsed, TimeSpan Uninstrumented, IReadOnlyList<Measurement> Measurements);

    private sealed record RecordedPhase(string Name, TimeSpan Start, TimeSpan End, bool Replay, List<RecordedPhase> Children);

    private sealed class Phase(string name, PerformanceTimeline timeline, Phase? parent, TimeSpan start, bool replay) : IDisposable
    {
        public string Name { get; } = name;
        public Phase? Parent { get; } = parent;
        public TimeSpan Start { get; } = start;
        public TimeSpan? End { get; private set; }
        public bool IsClosed => End.HasValue;
        public Activity? Activity { get; set; }

        public List<Phase> Children { get; } = new();

        public RecordedPhase Capture(TimeSpan elapsed) =>
            new(Name, Start, End ?? elapsed, replay, Children.Select(p => p.Capture(elapsed)).ToList());

        public void Dispose()
        {
            lock (timeline._lock)
            {
                if (IsClosed) return;
                End = timeline.Elapsed;
                if (ReferenceEquals(timeline._current.Value, this))
                {
                    timeline._current.Value = Parent;
                }
            }
            Activity?.SetEndTime(timeline.StartedAt + End.Value);
            Activity?.Dispose();
        }
    }
}
