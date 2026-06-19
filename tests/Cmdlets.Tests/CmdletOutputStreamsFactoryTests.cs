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

    [Fact]
    public void ShouldEmit_ShouldApplyExpectedMatrix()
    {
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Error, LogSeverity.Error));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Error, LogSeverity.Warn));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Error, LogSeverity.Info));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Error, LogSeverity.Debug));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Error, LogSeverity.Verbose));

        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Warn, LogSeverity.Error));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Warn, LogSeverity.Warn));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Warn, LogSeverity.Info));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Warn, LogSeverity.Debug));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Warn, LogSeverity.Verbose));

        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Info, LogSeverity.Error));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Info, LogSeverity.Warn));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Info, LogSeverity.Info));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Info, LogSeverity.Debug));
        Assert.False(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Info, LogSeverity.Verbose));

        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Debug, LogSeverity.Error));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Debug, LogSeverity.Warn));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Debug, LogSeverity.Info));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Debug, LogSeverity.Debug));
        Assert.True(CmdletOutputStreamsFactory.ShouldEmit(LogLevel.Debug, LogSeverity.Verbose));
    }
}
