// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Core.Telemetry.Tests;

public class LocalOtlpEndpointResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ReturnsDefault_WhenEnvUnsetOrBlank(string? value)
    {
        var result = LocalOtlpEndpointResolver.Resolve(_ => value);

        Assert.Equal(new Uri(WellKnownEndpoints.LocalOtlp), result);
    }

    [Theory]
    [InlineData("14317", "http://localhost:14317/")]
    [InlineData("  14317  ", "http://localhost:14317/")]
    [InlineData("4318", "http://localhost:4318/")]
    public void Resolve_UsesPort_WhenValid(string value, string expectedAbsoluteUri)
    {
        var result = LocalOtlpEndpointResolver.Resolve(_ => value);

        Assert.Equal(expectedAbsoluteUri, result.AbsoluteUri);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("70000")]
    [InlineData("abc")]
    [InlineData("43.17")]
    [InlineData("4317abc")]
    public void Resolve_FallsBackToDefault_WhenPortInvalid(string value)
    {
        var result = LocalOtlpEndpointResolver.Resolve(_ => value);

        Assert.Equal(new Uri(WellKnownEndpoints.LocalOtlp), result);
    }
}
