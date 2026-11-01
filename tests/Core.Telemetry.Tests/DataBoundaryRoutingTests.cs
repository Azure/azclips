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
    [InlineData("ROW")]
    [InlineData("row")]
    [InlineData("Global")]
    [InlineData("global")]
    [InlineData("NotDefined")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("APAC")]
    public void Normalize_ReturnsRow_ForEveryNonEuBoundary(string? dataBoundary) =>
        Assert.Equal(DataBoundaryRouting.Row, DataBoundaryRouting.Normalize(dataBoundary));

    [Fact]
    public void IsEu_IsTrue_OnlyForEu()
    {
        Assert.True(DataBoundaryRouting.IsEu("EU"));
        Assert.True(DataBoundaryRouting.IsEu("eu"));
        Assert.False(DataBoundaryRouting.IsEu("Global"));
        Assert.False(DataBoundaryRouting.IsEu("ROW"));
        Assert.False(DataBoundaryRouting.IsEu(null));
        Assert.False(DataBoundaryRouting.IsEu("APAC"));
    }
}
