// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Common;

namespace Core.Tests;

public class ProcessUtilsTests
{
    [Fact]
    public void GetRunningProcessName_ShouldReturnCurrentProcessName_WhenCalled()
    {
        using var current = Process.GetCurrentProcess();
        var expected = current.ProcessName;

        var actual = ProcessUtils.GetRunningProcessName();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetRunningProcessName_ShouldNotReturnUnknown_WhenProcessNameAvailable()
    {
        // On normal test hosts, ProcessName resolves successfully, so we should never see
        // the "unknown" fallback branch.
        var name = ProcessUtils.GetRunningProcessName();

        Assert.NotEqual("unknown", name);
    }

    [Fact]
    public void GetRunningProcessName_ShouldNotThrow_WhenCalled()
    {
        var exception = Record.Exception(() => ProcessUtils.GetRunningProcessName());

        Assert.Null(exception);
    }
}
