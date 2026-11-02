// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class DataBoundaryRoutingPolicyTests
{
    [Theory]
    [InlineData(DataBoundaryOutcome.TenantUnavailable, RegionalDestination.Row)]
    [InlineData(DataBoundaryOutcome.AuthenticationUnavailable, RegionalDestination.Row)]
    [InlineData(DataBoundaryOutcome.BoundaryUnavailable, RegionalDestination.Eu)]
    internal void Default_UsesExplicitScenarioDestination(DataBoundaryOutcome outcome, RegionalDestination expected)
    {
        Assert.Equal(expected, DataBoundaryRouting.ResolveDestination(null, outcome.ToString()));
        Assert.Equal(expected, DataBoundaryRouting.ResolveDestination("Unknown", outcome.ToString()));
        var exporter = TelemetryExporters.ResolveAriaExporter("Unknown", outcome.ToString());
        Assert.Equal(expected == RegionalDestination.Row
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
        Assert.Equal(RegionalDestination.Eu, DataBoundaryRouting.ResolveDestination("Unknown", outcome));
    }

    [Fact]
    public void CustomPolicy_ControlsEachFallbackWithoutChangingConfirmedBoundaries()
    {
        var policy = new DataBoundaryRoutingPolicy
        {
            TenantUnavailable = RegionalDestination.Eu,
            AuthenticationUnavailable = RegionalDestination.Eu,
            BoundaryUnavailable = RegionalDestination.Row,
        };
        Assert.Equal(RegionalDestination.Eu, policy.Resolve(null, DataBoundaryOutcome.TenantUnavailable));
        Assert.Equal(RegionalDestination.Eu, policy.Resolve(null, DataBoundaryOutcome.AuthenticationUnavailable));
        Assert.Equal(RegionalDestination.Row, policy.Resolve(null, DataBoundaryOutcome.BoundaryUnavailable));
        foreach (var outcome in Enum.GetValues<DataBoundaryOutcome>())
        {
            Assert.Equal(RegionalDestination.Eu, policy.Resolve("eu", outcome));
            Assert.Equal(RegionalDestination.Row, policy.Resolve("global", outcome));
        }
    }
}
