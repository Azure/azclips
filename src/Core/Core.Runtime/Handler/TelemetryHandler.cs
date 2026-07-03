// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler
{
    using System.Diagnostics;

    /// <summary>
    /// Marker interface for telemetry handlers.
    /// </summary>
    public class TelemetryHandler: ITelemetryHandler
    {
        public StartActivityDelegate StartActivity { get; set; } = null!;
    }
}