// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TracerProviderExportSinkTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void Constructor_CreatesSink_ForEachTarget(TelemetryTarget target)
    {
        // Construction only. Exercising TryExport here would build a live provider and
        // block on ForceFlush; TraceUploaderTests covers the behavior through a fake.
        ITraceExportSink sink = new TracerProviderExportSink(target);

        Assert.NotNull(sink);
    }
}
