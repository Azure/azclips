using Core.Agent;
using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="SpinnerService"/>. Assert on the renderer's
/// frame/stop callback sequence, not stdout text.
/// </summary>
public class SpinnerServiceTests
{
    private sealed class FakeRenderer : IAgentRenderer
    {
        public int FrameCalls;
        public int StopCalls;
        public void OnText(string chunk) { }
        public void OnEvent(AgentEvent ev) { }
        public void OnSpinnerFrame(string frame, string verb) => Interlocked.Increment(ref FrameCalls);
        public void OnSpinnerStop() => Interlocked.Increment(ref StopCalls);
    }

    [Fact]
    public async Task Start_EmitsFrames()
    {
        var fake = new FakeRenderer();
        var spinner = new SpinnerService(fake, TimeSpan.FromMilliseconds(10));

        spinner.Start();
        await Task.Delay(80);
        spinner.Stop();

        Assert.True(fake.FrameCalls > 0, "spinner produced no frames");
    }

    [Fact]
    public async Task Pause_StopsFrameEmission_ResumeRestarts()
    {
        var fake = new FakeRenderer();
        var spinner = new SpinnerService(fake, TimeSpan.FromMilliseconds(10));

        spinner.Start();
        await Task.Delay(60);
        spinner.Pause();
        var beforePause = fake.FrameCalls;
        var stopAfterPause = fake.StopCalls;

        await Task.Delay(80);
        var afterPause = fake.FrameCalls;
        Assert.Equal(beforePause, afterPause);
        Assert.True(stopAfterPause >= 1, "Pause must clear the spinner line");

        spinner.Resume();
        await Task.Delay(80);
        spinner.Stop();
        Assert.True(fake.FrameCalls > afterPause, "Resume must re-emit frames");
    }

    [Fact]
    public void Pause_BeforeStart_IsNoOp()
    {
        var fake = new FakeRenderer();
        var spinner = new SpinnerService(fake, TimeSpan.FromMilliseconds(10));

        spinner.Pause();
        spinner.Resume();

        Assert.Equal(0, fake.FrameCalls);
    }
}
