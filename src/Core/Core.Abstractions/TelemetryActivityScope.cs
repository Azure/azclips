// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Abstractions;

/// <summary>Owns an activity and, when requested, restores its caller's ambient context.</summary>
public sealed class TelemetryActivityScope(
    Activity activity,
    Activity? previousActivity = null,
    bool restoreAmbient = false,
    bool activated = true) : IDisposable
{
    private int _disposed;

    public Activity Activity { get; } = activity ?? throw new ArgumentNullException(nameof(activity));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var current = System.Diagnostics.Activity.Current;
        try
        {
            Activity.Dispose();
        }
        finally
        {
            if (restoreAmbient)
            {
                var restore = activated && (ReferenceEquals(current, Activity) || current is null)
                    ? previousActivity
                    : current;
                System.Diagnostics.Activity.Current = restore is { IsStopped: true } ? null : restore;
            }
        }
    }
}
