// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime;

/// <summary>Records local command timings independently of telemetry and logging policy.</summary>
public sealed class CommandTimingRecorder
{
    private readonly TimeProvider _clock;
    private readonly long _origin;
    private readonly object _lock = new();
    private readonly List<Phase> _roots = new();
    private readonly AsyncLocal<Phase?> _current = new();

    public CommandTimingRecorder(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _origin = _clock.GetTimestamp();
    }

    private TimeSpan Elapsed => _clock.GetElapsedTime(_origin, _clock.GetTimestamp());

    public IDisposable MeasureLocal(string name)
    {
        lock (_lock)
        {
            var parent = _current.Value;
            while (parent is { IsClosed: true })
            {
                parent = parent.Parent;
            }

            var phase = new Phase(name, this, parent, Elapsed);
            (parent?.Children ?? _roots).Add(phase);
            _current.Value = phase;
            return phase;
        }
    }

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

    private sealed class Phase(string name, CommandTimingRecorder recorder, Phase? parent, TimeSpan start) : IDisposable
    {
        public string Name { get; } = name;
        public Phase? Parent { get; } = parent;
        public TimeSpan Start { get; } = start;
        public TimeSpan? End { get; private set; }
        public bool IsClosed => End.HasValue;
        public List<Phase> Children { get; } = new();

        public void Dispose()
        {
            lock (recorder._lock)
            {
                if (IsClosed) return;
                End = recorder.Elapsed;
                if (ReferenceEquals(recorder._current.Value, this))
                {
                    recorder._current.Value = Parent;
                }
            }
        }
    }
}
