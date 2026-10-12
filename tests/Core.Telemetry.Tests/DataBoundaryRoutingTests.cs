// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class DataBoundaryRoutingTests
{
    [Theory]
    [InlineData("EU")]
    [InlineData("eu")]
    public void Normalize_ReturnsEu_ForEuLiteral(string dataBoundary) =>
        Assert.Equal(DataBoundaryRouting.Eu, DataBoundaryRouting.Normalize(dataBoundary));

    [Theory]
    [InlineData("Global")]
    [InlineData("global")]
    public void Normalize_ReturnsGlobal_ForGlobalLiteral(string dataBoundary) =>
        Assert.Equal(DataBoundaryRouting.Global, DataBoundaryRouting.Normalize(dataBoundary));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsUnknown_ForMissingValue(string? dataBoundary) =>
        Assert.Equal(DataBoundaryRouting.Unknown, DataBoundaryRouting.Normalize(dataBoundary));

    [Fact]
    public void Normalize_PreservesRawValue_ForUnrecognizedBoundary()
    {
        // An unrecognized/future ARM value must stay visible/groupable in telemetry rather
        // than being silently rounded to Global.
        Assert.Equal("APAC", DataBoundaryRouting.Normalize("APAC"));
    }

    [Fact]
    public void IsEu_IsTrue_OnlyForEu()
    {
        Assert.True(DataBoundaryRouting.IsEu("EU"));
        Assert.False(DataBoundaryRouting.IsEu("Global"));
        Assert.False(DataBoundaryRouting.IsEu(null));
        Assert.False(DataBoundaryRouting.IsEu("APAC"));
    }

    [Fact]
    public void IsConfirmedGlobal_IsTrue_OnlyForConfirmedGlobal()
    {
        Assert.True(DataBoundaryRouting.IsConfirmedGlobal("Global"));
        Assert.False(DataBoundaryRouting.IsConfirmedGlobal("EU"));

        // The crux of the fix: a missing/unresolved lookup and an unrecognized/future value
        // must NEVER be confused with ARM's genuine "Global" answer.
        Assert.False(DataBoundaryRouting.IsConfirmedGlobal(null));
        Assert.False(DataBoundaryRouting.IsConfirmedGlobal(""));
        Assert.False(DataBoundaryRouting.IsConfirmedGlobal("APAC"));
    }
}
