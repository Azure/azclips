// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Moq;

namespace Cmdlets.Tests;

public class ScopedLoggingServiceTests
{
    [Fact]
    public void Fallback_SuppressesProfileVerboseAtWarn()
    {
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(Configuration("warn"), sink);

        logger.Verbose("[ProfileService] Loading profiles");
        logger.Warn("warning");

        Assert.Empty(sink.VerboseMessages);
        Assert.Equal(["warning"], sink.Warnings);
    }

    [Fact]
    public void Fallback_EmitsProfileVerboseAtDebug()
    {
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(Configuration("debug"), sink);

        logger.Verbose("[ProfileService] Loading profiles");

        Assert.Equal(["[ProfileService] Loading profiles"], sink.VerboseMessages);
    }

    [Fact]
    public void ActiveScope_BypassesFallbackConfiguration()
    {
        var fallback = new RecordingLoggingService();
        var scoped = new RecordingLoggingService();
        var logger = new ScopedLoggingService(Configuration("warn"), fallback);

        using (ScopedLoggingService.UseScope(scoped))
        {
            logger.Verbose("command verbose");
        }

        Assert.Empty(fallback.VerboseMessages);
        Assert.Equal(["command verbose"], scoped.VerboseMessages);
    }

    private static IConfigurationService Configuration(string level)
    {
        var configuration = new Mock<IConfigurationService>();
        configuration
            .Setup(service => service.GetConfiguration<CoreOptions>(null, null))
            .Returns(new CoreOptions { Logging = new LoggingOptions { Level = level } });
        return configuration.Object;
    }

    private sealed class RecordingLoggingService : ILoggingService
    {
        public List<string> VerboseMessages { get; } = [];
        public List<string> Warnings { get; } = [];

        public void Log(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) => VerboseMessages.Add(message);
        public void Debug(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
