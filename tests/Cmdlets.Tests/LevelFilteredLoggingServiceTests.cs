// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;

namespace Cmdlets.Tests;

public class LevelFilteredLoggingServiceTests
{
    [Fact]
    public void WarnLevel_SuppressesVerboseAndDebug()
    {
        var inner = new RecordingLoggingService();
        var logger = new LevelFilteredLoggingService(inner, "warn");

        logger.Verbose("verbose");
        logger.Debug("debug");
        logger.Info("info");
        logger.Warn("warn");
        logger.Error("error");

        Assert.Equal(["warn"], inner.Warnings);
        Assert.Equal(["error"], inner.Errors);
        Assert.Empty(inner.VerboseMessages);
        Assert.Empty(inner.DebugMessages);
        Assert.Empty(inner.Information);
    }

    [Fact]
    public void DebugLevel_EmitsVerboseAndDebug()
    {
        var inner = new RecordingLoggingService();
        var logger = new LevelFilteredLoggingService(inner, "debug");

        logger.Verbose("verbose");
        logger.Debug("debug");

        Assert.Equal(["verbose"], inner.VerboseMessages);
        Assert.Equal(["debug"], inner.DebugMessages);
    }

    private sealed class RecordingLoggingService : ILoggingService
    {
        public List<string> Information { get; } = [];
        public List<string> VerboseMessages { get; } = [];
        public List<string> DebugMessages { get; } = [];
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];

        public void Log(string message) => Information.Add(message);
        public void Info(string message) => Information.Add(message);
        public void Verbose(string message) => VerboseMessages.Add(message);
        public void Debug(string message) => DebugMessages.Add(message);
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Errors.Add(message);
    }
}
