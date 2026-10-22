// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Cli.Tests;

public class LoggingPolicyTests
{
    [Theory]
    [InlineData("error", LogLevel.Error)]
    [InlineData("warn", LogLevel.Warn)]
    [InlineData("info", LogLevel.Info)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData(" DEBUG ", LogLevel.Debug)]
    [InlineData("Warn", LogLevel.Warn)]
    [InlineData(null, LogLevel.Warn)]
    [InlineData("", LogLevel.Warn)]
    [InlineData("  ", LogLevel.Warn)]
    [InlineData("invalid", LogLevel.Warn)]
    [InlineData("verbose", LogLevel.Warn)]
    public void Parse_ReturnsExpectedLevel(string? configuredValue, LogLevel expected)
    {
        Assert.Equal(expected, LoggingPolicy.Parse(configuredValue));
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
    public void IsEnabled_AppliesBaselineMatrix(LogLevel level, LogSeverity severity, bool expected)
    {
        Assert.Equal(expected, LoggingPolicy.IsEnabled(level, severity));
    }

    [Fact]
    public void IsEnabled_SuppressesUndefinedSeverity()
    {
        Assert.False(LoggingPolicy.IsEnabled(LogLevel.Debug, (LogSeverity)99));
    }
}
