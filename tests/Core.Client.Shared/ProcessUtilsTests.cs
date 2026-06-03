using System.Diagnostics;

namespace Core.Client.Shared.Tests;

public class ProcessUtilsTests
{
    [Fact]
    public void GetRunningProcess_ShouldReturnCurrentProcessName_WhenProcessIsAccessible()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var expected = currentProcess.ProcessName;

        var result = ProcessUtils.GetRunningProcess();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetRunningProcess_ShouldNotReturnUnknown_WhenProcessIsAccessible()
    {
        var result = ProcessUtils.GetRunningProcess();

        Assert.NotEqual("unknown", result);
    }

    [Fact]
    public void GetRunningProcess_ShouldReturnConsistentValue_WhenCalledMultipleTimes()
    {
        var first = ProcessUtils.GetRunningProcess();
        var second = ProcessUtils.GetRunningProcess();
        var third = ProcessUtils.GetRunningProcess();

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }
}

