// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Agent.Providers;
using Xunit;

namespace Core.Agent.Tests;

public class BuiltinEndpointResolverTests
{
    private const string EastUsHost = "azclis-copilot-apim-ppe-eus.azure-api.net";
    private const string WestEuropeHost = "azclis-copilot-apim-ppe-weu.azure-api.net";

    [Theory]
    [InlineData("Global")]
    [InlineData("global")]
    [InlineData("GLOBAL")]
    [InlineData("ROW")]
    [InlineData("row")]
    public void GlobalBoundaryOrRowDestination_UsesEastUs(string boundary)
    {
        var endpoint = BuiltinEndpointResolver.Choose(boundary);

        Assert.Equal(EastUsHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(boundary, endpoint.Boundary);
    }

    [Theory]
    [InlineData("EU", "EU")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("SomethingElse", "SomethingElse")]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    public void UnconfirmedBoundary_UsesWestEurope(string? boundary, string recorded)
    {
        var endpoint = BuiltinEndpointResolver.Choose(boundary);

        Assert.Equal(WestEuropeHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(recorded, endpoint.Boundary);
    }
}
