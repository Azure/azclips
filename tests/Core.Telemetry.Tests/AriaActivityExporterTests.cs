// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
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
            activity.SetTag("command", "aria-integration-test");
            activity.SetTag("command.exit_code", 0);
            activity.SetTag("InstallationId", "test-install-id");
            activity.SetTag("productVersion", "0.1.0-test");
            Thread.Sleep(50);
        }

        // Assert — flush succeeds (data reaches OneCollector endpoint)
        _output.WriteLine("Flushing to Aria (Global)...");
        var flushed = provider.ForceFlush(timeoutMilliseconds: 15_000);
        _output.WriteLine($"Flush result: {flushed}");

        Assert.True(flushed, "ForceFlush should succeed — OneCollector should accept the payload");
        _output.WriteLine("Done! Check Aria/Kusto for table Azclips_CommandExecution (Global)");
    }

    [Fact]
    public void Export_SendsActivityToAriaEu()
    {
        // Arrange — send to EU endpoint with faked EU connection string
        var exporter = new AriaActivityExporter(
            WellKnownEndpoints.AriaEuConnectionString,
            WellKnownEndpoints.AriaEuEndpoint);
        var source = new ActivitySource("AriaEuIntegrationTest");
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource("AriaEuIntegrationTest")
            .AddProcessor(new SimpleActivityExportProcessor(exporter));

        using var provider = builder.Build()!;

        // Act — create a test activity for EU boundary
        _output.WriteLine("Creating test activity for EU boundary...");
        using (var activity = source.StartActivity("TestCommandEu", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag("command", "aria-integration-test-eu");
            activity.SetTag("command.exit_code", 0);
            activity.SetTag("InstallationId", "test-install-id-eu");
            activity.SetTag("productVersion", "0.1.0-test-eu");
            activity.SetTag("DataBoundary", "EU");
            Thread.Sleep(50);
        }

        // Assert — flush to EU endpoint
        _output.WriteLine("Flushing to Aria (EU)...");
        var flushed = provider.ForceFlush(timeoutMilliseconds: 15_000);
        _output.WriteLine($"Flush result: {flushed}");

        Assert.True(flushed, "ForceFlush should succeed — OneCollector should accept the payload");
        _output.WriteLine("Done! Check Aria/Kusto for table Azclips_CommandExecution (EU)");
    }

    [Fact]
    public void Export_RoutesDynamically_BasedOnPerActivityDataBoundaryTag()
    {
        // Arrange — one long-lived exporter instance, as PowerShell's singleton
        // TracerProvider would build it, serving commands with different boundaries
        // over its lifetime instead of freezing the destination at construction time.
        var exporter = new AriaActivityExporter();
        var source = new ActivitySource("AriaDynamicRoutingTest");
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource("AriaDynamicRoutingTest")
            .AddProcessor(new SimpleActivityExportProcessor(exporter));

        using var provider = builder.Build()!;

        // Act — a Global command, then an EU command, on the same exporter instance. The
        // destination is resolved per exported Activity from its own DataBoundary tag (set
        // directly on the activity here — production code tags the root activity the same
        // way via ITelemetryService.AddTag), not from ambient async-local state.
        using (var activity = source.StartActivity("DynamicGlobalCommand", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag("command", "dynamic-routing-global");
            activity.SetTag(TelemetryTags.DataBoundary, "Global");
        }

        using (var activity = source.StartActivity("DynamicEuCommand", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag("command", "dynamic-routing-eu");
            activity.SetTag(TelemetryTags.DataBoundary, "EU");
        }

        // Assert — both routes flush successfully from the one exporter instance.
        _output.WriteLine("Flushing dynamically-routed Global then EU activities...");
        var flushed = provider.ForceFlush(timeoutMilliseconds: 15_000);
        _output.WriteLine($"Flush result: {flushed}");

        Assert.True(flushed, "ForceFlush should succeed for both Global and EU exports from one long-lived exporter");
    }
}
