// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class DataBoundaryRoutingPolicyTests
{
    [Theory]
    [InlineData("TenantUnavailable", UploadRegion.ROW)]
    [InlineData("AuthenticationUnavailable", UploadRegion.ROW)]
    [InlineData("BoundaryUnavailable", UploadRegion.EU)]
    public void Default_UsesExplicitScenarioDestination(string outcome, UploadRegion expected)
    {
        var region = DataBoundaryRoutingPolicy.Default.Resolve(null, Enum.Parse<DataBoundaryOutcome>(outcome));
        Assert.Equal(expected, region);
        var exporter = TelemetryExporters.ResolveAriaExporter(region);
        Assert.Equal(expected == UploadRegion.ROW
            ? WellKnownEndpoints.AriaRowEndpoint : WellKnownEndpoints.AriaEuEndpoint, exporter.Endpoint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FutureOutcome")]
    [InlineData("999")]
    [InlineData("1")]
    [InlineData("Confirmed")]
    public void UnclassifiedUnknown_UsesLegacyEuDestination(string? outcome)
    {
        Assert.Equal(UploadRegion.EU, UploadRegions.Resolve(null, "Unknown", outcome));
    }

    [Fact]
    public void CustomPolicy_ControlsEachFallbackWithoutChangingConfirmedBoundaries()
    {
        var policy = new DataBoundaryRoutingPolicy
        {
            TenantUnavailable = UploadRegion.EU,
            AuthenticationUnavailable = UploadRegion.EU,
            BoundaryUnavailable = UploadRegion.ROW,
        };
        Assert.Equal(UploadRegion.EU, policy.Resolve(null, DataBoundaryOutcome.TenantUnavailable));
        Assert.Equal(UploadRegion.EU, policy.Resolve(null, DataBoundaryOutcome.AuthenticationUnavailable));
        Assert.Equal(UploadRegion.ROW, policy.Resolve(null, DataBoundaryOutcome.BoundaryUnavailable));
        foreach (var outcome in Enum.GetValues<DataBoundaryOutcome>())
        {
            Assert.Equal(UploadRegion.EU, policy.Resolve("eu", outcome));
            Assert.Equal(UploadRegion.ROW, policy.Resolve("global", outcome));
        }
    }
}
