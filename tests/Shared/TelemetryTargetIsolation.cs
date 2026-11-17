// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace Tests.Shared;

internal static class TelemetryTargetIsolation
{
    // Debug builds cache AZCLIPS_TELEMETRY_TARGET on first use, and "local" switches the whole
    // process to the unfiltered pipeline. Clear a developer's shell setting before any test runs,
    // matching CI, which never sets it.
#pragma warning disable CA2255 // A test assembly owns its process, like an application.
    [ModuleInitializer]
    internal static void ClearTelemetryTarget() =>
        Environment.SetEnvironmentVariable("AZCLIPS_TELEMETRY_TARGET", null);
#pragma warning restore CA2255
}
