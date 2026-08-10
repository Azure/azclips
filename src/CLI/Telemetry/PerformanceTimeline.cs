// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Cli.Telemetry;

/// <summary>
/// Times startup phases that run before the telemetry stack exists.
///
/// The <see cref="ActivitySource"/> and the listener that exports it are both created
/// while DI resolves <c>ITelemetryProvider</c>, so <c>StartActivity</c> returns
/// <see langword="null"/> until then — startup cannot be traced live. This type notes
/// <see cref="DateTime.UtcNow"/>, the same clock <c>Activity.Start()</c> uses, and
/// <see cref="Replay"/> materialises the recorded phases as spans once telemetry is
/// available. Same clock, read early and used late.
/// </summary>
internal sealed class PerformanceTimeline
{
    /// <summary>Name of the span that encloses every recorded phase.</summary>
    private const string EnclosingSpanName = "CLI.Performance";

    private readonly List<Phase> _roots = new();
    private readonly Stack<Phase> _open = new();

    /// <summary>
    /// When the timeline began. Used to backdate the root activity so the replayed
    /// phases nest beneath it rather than starting before it.
    /// </summary>
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Times the enclosing block. Nested calls produce nested spans; sequential calls
    /// produce siblings.
    /// </summary>
    public IDisposable Measure(string name)
    {
        var phase = new Phase(name, this);
        (_open.Count > 0 ? _open.Peek().Children : _roots).Add(phase);
        _open.Push(phase);
        return phase;
    }

    /// <summary>
    /// Replays the recorded phases as spans beneath the current activity. Call once the
    /// root activity has started. No-op when nothing was measured, and safe when
    /// telemetry is disabled — every <c>StartActivity</c> then returns null.
    /// </summary>
    public void Replay(ITelemetryService telemetry)
    {
        if (_roots.Count == 0) return;

        using var enclosing = telemetry.StartActivity(EnclosingSpanName, ActivityKind.Internal, StartedAt);
        foreach (var root in _roots)
        {
            Emit(telemetry, root);
        }

        // The last top-level phase to be recorded is the last one to finish.
        enclosing?.SetEndTime(_roots[^1].End);
    }

    private static void Emit(ITelemetryService telemetry, Phase phase)
    {
        // The span stays current while its children are created, so the ambient
        // Activity.Current parenting reproduces the recorded nesting.
        using var span = telemetry.StartActivity(phase.Name, ActivityKind.Internal, phase.Start);
        foreach (var child in phase.Children)
        {
            Emit(telemetry, child);
        }

        span?.SetEndTime(phase.End);
    }

    private sealed class Phase : IDisposable
    {
        private readonly PerformanceTimeline _timeline;
        private bool _disposed;

        public Phase(string name, PerformanceTimeline timeline)
        {
            Name = name;
            _timeline = timeline;
        }

        public string Name { get; }

        public DateTime Start { get; } = DateTime.UtcNow;

        public DateTime End { get; private set; }

        public List<Phase> Children { get; } = new();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            End = DateTime.UtcNow;
            _timeline._open.Pop();
        }
    }
}
