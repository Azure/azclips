// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class TelemetryExportersTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void AddTraceExporter_BuildsProvider_WithoutThrowing(TelemetryTarget target)
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporter(builder, target);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }
}
