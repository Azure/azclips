// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;

namespace Core.Telemetry;

/// <summary>
/// Resolves the local OTLP endpoint the app exports to when the telemetry target
/// is <see cref="TelemetryTarget.Local"/>. Defaults to
/// <see cref="WellKnownEndpoints.LocalOtlp"/>; a developer can move it to a
/// different port (for example when the default is already in use) by setting the
/// <c>AZCLIPS_OTLP_PORT</c> environment variable.
/// </summary>
internal static class LocalOtlpEndpointResolver
{
    internal const string PortEnvironmentVariable = "AZCLIPS_OTLP_PORT";

    /// <summary>Resolves the endpoint using the process environment.</summary>
    internal static Uri Resolve() => Resolve(Environment.GetEnvironmentVariable);

    internal static Uri Resolve(Func<string, string?> environmentReader)
    {
        var defaultEndpoint = new Uri(WellKnownEndpoints.LocalOtlp);

        var raw = environmentReader(PortEnvironmentVariable)?.Trim();
        if (!string.IsNullOrEmpty(raw)
            && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= 1 and <= 65535)
        {
            return new UriBuilder(defaultEndpoint) { Port = port }.Uri;
        }

        return defaultEndpoint;
    }
}
