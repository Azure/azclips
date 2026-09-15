// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Abstracts how a client exports telemetry: buffered to file, or inline.</summary>
public interface ITelemetrySender
{
    /// <summary>Adds the appropriate exporter(s) to the TracerProvider being built.</summary>
    void ConfigureExporter(TracerProviderBuilder builder);
}
