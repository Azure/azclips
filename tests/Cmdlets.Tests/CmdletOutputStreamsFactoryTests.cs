// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Cmdlets.Tests;

public class CmdletOutputStreamsFactoryTests
{
    [Theory]
    [InlineData("error", 0)]
    [InlineData("warn", 1)]
    [InlineData("info", 2)]
    [InlineData("debug", 3)]
    [InlineData("DEBUG", 3)]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("invalid", 1)]
    public void Parse_ShouldReturnExpectedLevel(string? configuredValue, int expected)
    {
        var level = CmdletOutputStreamsFactory.Parse(configuredValue);

        Assert.Equal((LogLevel)expected, level);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(0, 2, false)]
    [InlineData(0, 3, false)]
    [InlineData(0, 4, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(1, 3, false)]
    [InlineData(1, 4, false)]
    [InlineData(2, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    [InlineData(2, 4, false)]
    [InlineData(3, 0, true)]
    [InlineData(3, 1, true)]
    [InlineData(3, 2, true)]
    [InlineData(3, 3, true)]
    [InlineData(3, 4, true)]
    public void ShouldEmit_ShouldApplyExpectedMatrix(int level, int severity, bool expected)
    {
        Assert.Equal(expected, CmdletOutputStreamsFactory.ShouldEmit((LogLevel)level, (LogSeverity)severity));
    }

    [Theory]
    [InlineData("Debug", true, true)]
    [InlineData("Debug", false, false)]
    [InlineData("Verbose", true, true)]
    [InlineData("Verbose", false, false)]
    public void IsOverrideEnabled_ShouldHandleBooleanValues(string parameterName, bool parameterValue, bool expected)
    {
        var boundParameters = new Dictionary<string, object>
        {
            [parameterName] = parameterValue,
        };

        Assert.Equal(expected, CmdletOutputStreamsFactory.IsOverrideEnabled(boundParameters, parameterName));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void IsOverrideEnabled_ShouldHandleSwitchParameterValue(bool isPresent, bool expected)
    {
        var boundParameters = new Dictionary<string, object>
        {
            ["Debug"] = new SwitchParameter(isPresent),
        };

        Assert.Equal(expected, CmdletOutputStreamsFactory.IsOverrideEnabled(boundParameters, "Debug"));
    }

    [Fact]
    public void IsOverrideEnabled_ShouldReturnFalse_WhenParameterIsMissing()
    {
        var boundParameters = new Dictionary<string, object>();

        Assert.False(CmdletOutputStreamsFactory.IsOverrideEnabled(boundParameters, "Debug"));
    }
}
