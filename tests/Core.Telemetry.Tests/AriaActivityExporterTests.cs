// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace Core.Telemetry.Tests;

public class AriaActivityExporterTests
{
    private readonly ITestOutputHelper _output;

    public AriaActivityExporterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Export_SendsActivityToAria()
    {
        // Arrange
        var exporter = new AriaActivityExporter(WellKnownEndpoints.AriaConnectionString);
        var source = new ActivitySource("AriaIntegrationTest");
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource("AriaIntegrationTest")
            .AddProcessor(new SimpleActivityExportProcessor(exporter));

        using var provider = builder.Build()!;

        // Act — create a test activity with tags
        _output.WriteLine("Creating test activity...");
        using (var activity = source.StartActivity("TestCommand", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag("command.name", "aria-integration-test");
            activity.SetTag("command.exit_code", 0);
            activity.SetTag("installation_id", "test-install-id");
            activity.SetTag("product.version", "0.1.0-test");
            Thread.Sleep(50);
        }

        // Assert — flush succeeds (data reaches OneCollector endpoint)
        _output.WriteLine("Flushing to Aria...");
        var flushed = provider.ForceFlush(timeoutMilliseconds: 15_000);
        _output.WriteLine($"Flush result: {flushed}");

        Assert.True(flushed, "ForceFlush should succeed — OneCollector should accept the payload");
        _output.WriteLine("Done! Check Aria/Kusto for table Azclips_CommandExecution");
    }
}
