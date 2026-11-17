// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

internal static class TelemetryNetworkGuard
{
    private static readonly AsyncLocal<bool> Disabled = new();

    internal static IDisposable Disable()
    {
        var previous = Disabled.Value;
        Disabled.Value = true;
        return new GuardScope(previous);
    }

    internal static void EnsureAllowed()
    {
        if (Disabled.Value)
            throw new InvalidOperationException("Network telemetry exporters are disabled in this execution context.");
    }

    private sealed class GuardScope(bool previous) : IDisposable
    {
        public void Dispose() => Disabled.Value = previous;
    }
}
