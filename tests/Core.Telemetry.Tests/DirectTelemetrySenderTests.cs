// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class DirectTelemetrySenderTests
{
    [Fact]
    public void ConfigureExporter_BuildsProvider_WithoutThrowing()
    {
        var sender = new DirectTelemetrySender();
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        sender.ConfigureExporter(builder);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }
}
