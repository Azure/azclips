// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Where telemetry is exported when it is enabled. Internal by design — the
/// direction switch is a developer-only concern and is never surfaced in the
/// user-facing configuration (no schema key, no env-var mapping).
/// </summary>
internal enum TelemetryTarget
{
    /// <summary>Local dev stack (OTLP → Jaeger).</summary>
    Local,

    /// <summary>Remote Aria / OneCollector.</summary>
    Remote,
}
