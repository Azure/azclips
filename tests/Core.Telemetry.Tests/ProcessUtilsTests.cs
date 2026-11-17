// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Telemetry.Tests;

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

    [Theory]
    [InlineData("/bin/bash", false, "bash")]
    [InlineData("/usr/bin/fish", false, "fish")]
    [InlineData("/opt/homebrew/bin/zsh", false, "zsh")]
    [InlineData("pwsh.exe", true, "pwsh")]
    [InlineData(null, true, "pwsh")]
    [InlineData("", true, "pwsh")]
    [InlineData(null, false, "unknown")]
    [InlineData("/bin/sh", false, "unknown")]
    public void DetectShell_ShouldUseConfiguredShellOrPlatformFallback(
        string? configuredShell,
        bool isWindows,
        string expected)
    {
        Assert.Equal(expected, ProcessUtils.DetectShell(configuredShell, isWindows));
    }
}
