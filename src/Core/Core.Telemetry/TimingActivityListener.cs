// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Subscribes to <see cref="ActivitySource"/> events and prints each activity's
/// elapsed time as a tree, indented by parent depth. Reusable "decorator" for
/// timing — any code that already wraps work in
/// <c>using var _ = source.StartActivity("name")</c> shows up automatically
/// here, no per-call-site stopwatch wiring needed.
///
/// Opt-in: install only when env var <c>CIRRUS_TIMING=1</c> is set, so
/// regular CLI users don't see timing noise.
/// </summary>
public static class TimingActivityListener
{
    public const string EnvVar = "CIRRUS_TIMING";

    /// <summary>
    /// Returns true when timing output is requested via the
    /// <see cref="EnvVar"/> environment variable (any non-empty value).
    /// </summary>
    public static bool Enabled =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>
    /// Installs a process-wide listener that prints elapsed time for every
    /// stopped activity from sources whose name starts with one of
    /// <paramref name="sourcePrefixes"/>. Returns the listener so the caller
    /// can dispose it; safe to ignore — process exit cleans it up.
    /// </summary>
    public static IDisposable? Install(ILoggingService log, params string[] sourcePrefixes)
    {
        if (!Enabled) return null;

        var prefixes = sourcePrefixes is { Length: > 0 } ? sourcePrefixes : new[] { "" };
        var listener = new ActivityListener
        {
            ShouldListenTo = src => prefixes.Any(p => src.Name.StartsWith(p, StringComparison.Ordinal)),
            // AllData so Duration is populated on stop.
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                var depth = 0;
                for (var p = a.Parent; p is not null; p = p.Parent) depth++;
                var indent = new string(' ', depth * 2);
                log.Info($"[TIMING] {indent}{a.OperationName} {a.Duration.TotalMilliseconds:F0}ms");
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
