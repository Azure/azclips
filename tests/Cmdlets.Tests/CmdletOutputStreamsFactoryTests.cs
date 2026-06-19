using System.Management.Automation;
using Microsoft.Azure.Cirrus.Cmdlets.Common;

namespace Cmdlets.Tests;

public class CmdletOutputStreamsFactoryTests
{
    [Theory]
    [InlineData("error", 0)]
    [InlineData("warn", 1)]
    [InlineData("info", 2)]
    [InlineData("debug", 3)]
    [InlineData("DEBUG", 3)]
    [InlineData(null, 2)]
    [InlineData("", 2)]
    [InlineData("invalid", 2)]
    public void Parse_ShouldReturnExpectedLevel(string? configuredValue, int expected)
    {
        var level = CmdletOutputStreamsFactory.Parse(configuredValue);

        Assert.Equal((LogLevel)expected, level);
    }

    [Theory]
    [InlineData(LogLevel.Error, LogSeverity.Error, true)]
    [InlineData(LogLevel.Error, LogSeverity.Warn, false)]
    [InlineData(LogLevel.Error, LogSeverity.Info, false)]
    [InlineData(LogLevel.Error, LogSeverity.Debug, false)]
    [InlineData(LogLevel.Error, LogSeverity.Verbose, false)]
    [InlineData(LogLevel.Warn, LogSeverity.Error, true)]
    [InlineData(LogLevel.Warn, LogSeverity.Warn, true)]
    [InlineData(LogLevel.Warn, LogSeverity.Info, false)]
    [InlineData(LogLevel.Warn, LogSeverity.Debug, false)]
    [InlineData(LogLevel.Warn, LogSeverity.Verbose, false)]
    [InlineData(LogLevel.Info, LogSeverity.Error, true)]
    [InlineData(LogLevel.Info, LogSeverity.Warn, true)]
    [InlineData(LogLevel.Info, LogSeverity.Info, true)]
    [InlineData(LogLevel.Info, LogSeverity.Debug, false)]
    [InlineData(LogLevel.Info, LogSeverity.Verbose, false)]
    [InlineData(LogLevel.Debug, LogSeverity.Error, true)]
    [InlineData(LogLevel.Debug, LogSeverity.Warn, true)]
    [InlineData(LogLevel.Debug, LogSeverity.Info, true)]
    [InlineData(LogLevel.Debug, LogSeverity.Debug, true)]
    [InlineData(LogLevel.Debug, LogSeverity.Verbose, true)]
    public void ShouldEmit_ShouldApplyExpectedMatrix(LogLevel level, LogSeverity severity, bool expected)
    {
        Assert.Equal(expected, CmdletOutputStreamsFactory.ShouldEmit(level, severity));
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
