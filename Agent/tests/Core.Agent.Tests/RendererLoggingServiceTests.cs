using Core.Agent;
using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="RendererLoggingService"/>. Verifies the
/// "no error/warn is silently lost" guarantee: every Warn/Error call MUST
/// produce a HostLogEmitted event on the renderer; Verbose/Debug/Info/Log
/// are intentionally dropped (noise that would race the spinner).
/// </summary>
public class RendererLoggingServiceTests
{
    private sealed class CapturingRenderer : IAgentRenderer
    {
        public readonly List<AgentEvent> Events = new();
        public readonly List<string> Texts = new();
        public void OnText(string chunk) => Texts.Add(chunk);
        public void OnEvent(AgentEvent ev) => Events.Add(ev);
        public void OnSpinnerFrame(string frame, string verb) { }
        public void OnSpinnerStop() { }
    }

    [Fact]
    public void Error_EmitsHostLogEmittedEvent_AtErrorLevel()
    {
        var renderer = new CapturingRenderer();
        var log = new RendererLoggingService(renderer);

        log.Error("MSAL token acquire failed");

        var ev = Assert.Single(renderer.Events);
        var hl = Assert.IsType<HostLogEmitted>(ev);
        Assert.Equal(HostLogLevel.Error, hl.Level);
        Assert.Equal("MSAL token acquire failed", hl.Message);
    }

    [Fact]
    public void Warn_EmitsHostLogEmittedEvent_AtWarnLevel()
    {
        var renderer = new CapturingRenderer();
        var log = new RendererLoggingService(renderer);

        log.Warn("subscription cache stale");

        var hl = Assert.IsType<HostLogEmitted>(Assert.Single(renderer.Events));
        Assert.Equal(HostLogLevel.Warn, hl.Level);
        Assert.Equal("subscription cache stale", hl.Message);
    }

    [Theory]
    [InlineData("Verbose")]
    [InlineData("Debug")]
    [InlineData("Info")]
    [InlineData("Log")]
    public void NoiseLevels_AreDropped(string method)
    {
        var renderer = new CapturingRenderer();
        var log = new RendererLoggingService(renderer);

        switch (method)
        {
            case "Verbose": log.Verbose("v"); break;
            case "Debug":   log.Debug("d");   break;
            case "Info":    log.Info("i");    break;
            case "Log":     log.Log("l");     break;
        }

        Assert.Empty(renderer.Events);
        Assert.Empty(renderer.Texts);
    }

    [Fact]
    public void NullMessage_DoesNotThrow_AndEmitsEmpty()
    {
        var renderer = new CapturingRenderer();
        var log = new RendererLoggingService(renderer);

        log.Error(null!);
        log.Warn(null!);

        Assert.Equal(2, renderer.Events.Count);
        Assert.All(renderer.Events, e => Assert.Equal(string.Empty, ((HostLogEmitted)e).Message));
    }

    [Fact]
    public void HostLogEmitted_Error_UsesErrorRole()
    {
        var ev = new HostLogEmitted(HostLogLevel.Error, "boom", DateTimeOffset.UtcNow);

        var segments = AgentEventStyle.Format(ev);

        Assert.Equal(AgentColorRole.Error, segments[0].Role);
    }

    [Fact]
    public void HostLogEmitted_Warn_UsesWarningRole()
    {
        var ev = new HostLogEmitted(HostLogLevel.Warn, "careful", DateTimeOffset.UtcNow);

        var segments = AgentEventStyle.Format(ev);

        Assert.Equal(AgentColorRole.Warning, segments[0].Role);
    }
}
