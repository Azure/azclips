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
    // CI agents under load can stall 100ms+ between ticks, so positive-direction
    // waits ("a frame should arrive") use an event with a generous timeout
    // instead of a fixed Task.Delay. Negative-direction waits ("no frame should
    // arrive while paused") still need a wall-clock sleep — there is no event
    // to await on absence.
    private const int FrameWaitMs = 60000;
    private const int IntervalMs = 10;
    private const int PauseProbeMs = 80;

    private sealed class FakeRenderer : IAgentRenderer
    {
        public int FrameCalls;
        public int StopCalls;
        public readonly ManualResetEventSlim FrameSeen = new(false);
        public void OnText(string chunk) { }
        public void OnEvent(AgentEvent ev) { }
        public void OnSpinnerFrame(string frame, string verb)
        {
            Interlocked.Increment(ref FrameCalls);
            FrameSeen.Set();
        }
        public void OnSpinnerStop() => Interlocked.Increment(ref StopCalls);
    }

    [Fact]
    public void Start_EmitsFrames()
    {
        var fake = new FakeRenderer();
        var spinner = new SpinnerService(fake, TimeSpan.FromMilliseconds(IntervalMs));

        spinner.Start();
        Assert.True(fake.FrameSeen.Wait(FrameWaitMs), "spinner produced no frames within timeout");
        spinner.Stop();

        Assert.True(fake.FrameCalls > 0, "spinner produced no frames");
    }

    [Fact]
    public async Task Pause_StopsFrameEmission_ResumeRestarts()
    {
        var fake = new FakeRenderer();
        var spinner = new SpinnerService(fake, TimeSpan.FromMilliseconds(IntervalMs));

        spinner.Start();
        Assert.True(fake.FrameSeen.Wait(FrameWaitMs), "spinner produced no frames before pause");
        spinner.Pause();
        // A frame already in flight when Pause() returns lands within one interval.
        await Task.Delay(IntervalMs * 2);
        var beforePause = fake.FrameCalls;
        var stopAfterPause = fake.StopCalls;

        await Task.Delay(PauseProbeMs);
        var afterPause = fake.FrameCalls;
        Assert.Equal(beforePause, afterPause);
        Assert.True(stopAfterPause >= 1, "Pause must clear the spinner line");

        fake.FrameSeen.Reset();
        spinner.Resume();
        Assert.True(fake.FrameSeen.Wait(FrameWaitMs), "Resume produced no frames within timeout");
        spinner.Stop();
        Assert.True(fake.FrameCalls > afterPause, "Resume must re-emit frames");
    }

    [Fact]
    public void Pause_BeforeStart_IsNoOp()
    {
        var fake = new FakeRenderer();
        var spinner = new SpinnerService(fake, TimeSpan.FromMilliseconds(IntervalMs));

        spinner.Pause();
        spinner.Resume();

        Assert.Equal(0, fake.FrameCalls);
    }
}
