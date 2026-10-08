// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Cmdlets.Tests;

public class CmdletOutputStreamsFactoryTests
{
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
