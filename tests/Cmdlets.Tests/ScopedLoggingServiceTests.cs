// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

public class ScopedLoggingServiceTests
{
    [Fact]
    public void PublicParameterlessConstructor_IsPreserved()
    {
        Assert.NotNull(typeof(ScopedLoggingService).GetConstructor(Type.EmptyTypes));
    }

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
        var configuration = new Mock<IConfigurationService>(MockBehavior.Strict);
        var logger = new ScopedLoggingService(configuration.Object, fallback);

        using (ScopedLoggingService.UseScope(scoped))
        {
            logger.Verbose("command verbose");
        }

        Assert.Empty(fallback.VerboseMessages);
        Assert.Equal(["command verbose"], scoped.VerboseMessages);
        configuration.VerifyNoOtherCalls();
    }

    [Fact]
    public void Fallback_ErrorEmitsWhenConfigurationThrows()
    {
        var configuration = new Mock<IConfigurationService>();
        configuration
            .Setup(service => service.GetConfiguration<CoreOptions>(null, null))
            .Throws(new InvalidOperationException("invalid configuration"));
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(configuration.Object, sink);

        logger.Error("startup failed");

        Assert.Equal(["startup failed"], sink.Errors);
        configuration.Verify(service => service.GetConfiguration<CoreOptions>(null, null), Times.Never);
    }

    [Fact]
    public void Fallback_UsesWarnWhenConfigurationIsInvalidInput()
    {
        var configuration = new Mock<IConfigurationService>();
        configuration
            .Setup(service => service.GetConfiguration<CoreOptions>(null, null))
            .Throws(AzclipsErrorException.InvalidInput("Configuration file contains invalid JSON."));
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(configuration.Object, sink);

        logger.Verbose("profile details");
        logger.Warn("startup warning");
        logger.Error("startup error");

        Assert.Empty(sink.VerboseMessages);
        Assert.Equal(["startup warning"], sink.Warnings);
        Assert.Equal(["warn", "error"], sink.Calls);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Fallback_UsesWarnWhenConfigurationThrows(Type exceptionType)
    {
        var configuration = new Mock<IConfigurationService>();
        configuration
            .Setup(service => service.GetConfiguration<CoreOptions>(null, null))
            .Throws((Exception)Activator.CreateInstance(exceptionType)!);
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(configuration.Object, sink);

        logger.Verbose("profile details");
        logger.Debug("debug details");
        logger.Info("info details");
        logger.Log("general details");
        logger.Warn("startup warning");
        logger.Error("startup error");

        Assert.Empty(sink.VerboseMessages);
        Assert.Equal(["startup warning"], sink.Warnings);
        Assert.Equal(["warn", "error"], sink.Calls);
    }

    [Theory]
    [InlineData("error", "error")]
    [InlineData("warn", "error,warn")]
    [InlineData("info", "error,warn,info,log")]
    [InlineData("debug", "error,warn,info,log,debug,verbose")]
    [InlineData(" DEBUG ", "error,warn,info,log,debug,verbose")]
    [InlineData(null, "error,warn")]
    [InlineData("", "error,warn")]
    [InlineData("  ", "error,warn")]
    [InlineData("invalid", "error,warn")]
    public void Fallback_AppliesSharedThresholds(string? level, string expected)
    {
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(Configuration(level), sink);

        logger.Error("error");
        logger.Warn("warn");
        logger.Info("info");
        logger.Log("log");
        logger.Debug("debug");
        logger.Verbose("verbose");

        Assert.Equal(expected.Split(','), sink.Calls);
    }

    [Fact]
    public void Fallback_ObservesConfigurationChangesWithoutReconstruction()
    {
        var options = new CoreOptions();
        var configuration = new Mock<IConfigurationService>();
        configuration.Setup(service => service.GetConfiguration<CoreOptions>(null, null)).Returns(options);
        var sink = new RecordingLoggingService();
        var logger = new ScopedLoggingService(configuration.Object, sink);

        logger.Verbose("hidden at warn");
        options.Logging.Level = "debug";
        logger.Verbose("visible at debug");
        options.Logging.Level = "warn";
        logger.Verbose("hidden again");

        Assert.Equal(["visible at debug"], sink.VerboseMessages);
        configuration.Verify(service => service.GetConfiguration<CoreOptions>(null, null), Times.Exactly(3));
    }

    [Fact]
    public void NestedScopes_RestorePreviousLoggerAndFallback()
    {
        var fallback = new RecordingLoggingService();
        var outer = new RecordingLoggingService();
        var inner = new RecordingLoggingService();
        var logger = new ScopedLoggingService(Configuration("warn"), fallback);

        using (ScopedLoggingService.UseScope(outer))
        {
            logger.Verbose("outer");
            using (ScopedLoggingService.UseScope(inner))
            {
                logger.Verbose("inner");
            }
            logger.Verbose("outer restored");
        }
        logger.Verbose("fallback suppressed");
        logger.Warn("fallback restored");

        Assert.Equal(["outer", "outer restored"], outer.VerboseMessages);
        Assert.Equal(["inner"], inner.VerboseMessages);
        Assert.Empty(fallback.VerboseMessages);
        Assert.Equal(["fallback restored"], fallback.Warnings);
    }

    [Fact]
    public void DependencyInjection_UsesConfiguredConstructor()
    {
        var configuration = new Mock<IConfigurationService>();
        configuration.Setup(service => service.GetConfiguration<CoreOptions>(null, null)).Returns(new CoreOptions());
        var services = new ServiceCollection();
        services.AddSingleton(configuration.Object);
        services.AddSingleton<ILoggingService, ScopedLoggingService>();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ILoggingService>().Verbose("hidden");

        configuration.Verify(service => service.GetConfiguration<CoreOptions>(null, null), Times.Once);
    }

    [Theory]
    [InlineData("warn", false)]
    [InlineData("debug", true)]
    public void Fallback_RecordsActivityEventOnlyForEnabledMessage(string level, bool enabled)
    {
        using var activity = new Activity(nameof(Fallback_RecordsActivityEventOnlyForEnabledMessage)).Start();
        var logger = new ScopedLoggingService(Configuration(level));

        logger.Verbose("profile details");

        if (enabled)
        {
            var logEvent = Assert.Single(activity.Events);
            Assert.Equal("Verbose", logEvent.Name);
            Assert.Contains(logEvent.Tags, tag => tag.Key == "message" && Equals(tag.Value, "profile details"));
        }
        else
        {
            Assert.Empty(activity.Events);
        }
    }

    private static IConfigurationService Configuration(string? level)
    {
        var configuration = new Mock<IConfigurationService>();
        configuration
            .Setup(service => service.GetConfiguration<CoreOptions>(null, null))
            .Returns(level is null
                ? new CoreOptions()
                : new CoreOptions { Logging = new LoggingOptions { Level = level } });
        return configuration.Object;
    }

    private sealed class RecordingLoggingService : ILoggingService
    {
        public List<string> VerboseMessages { get; } = [];
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Calls { get; } = [];

        public void Log(string message) => Calls.Add("log");
        public void Info(string message) => Calls.Add("info");
        public void Verbose(string message)
        {
            VerboseMessages.Add(message);
            Calls.Add("verbose");
        }
        public void Debug(string message) => Calls.Add("debug");
        public void Warn(string message)
        {
            Warnings.Add(message);
            Calls.Add("warn");
        }
        public void Error(string message)
        {
            Errors.Add(message);
            Calls.Add("error");
        }
    }
}
