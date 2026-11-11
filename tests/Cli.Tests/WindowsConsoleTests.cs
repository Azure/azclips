// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Output;

namespace Cli.Tests;

public class WindowsConsoleTests
{
    [Theory]
    [InlineData(false, false, -11, -12)]
    [InlineData(true, false, -12)]
    [InlineData(false, true, -11)]
    [InlineData(true, true)]
    public void Initialize_SkipsRedirectedStreams(
        bool outputRedirected, bool errorRedirected, params int[] expectedHandles)
    {
        var requested = new List<int>();
        using var errors = new StringWriter();

        var supported = WindowsConsole.Initialize(outputRedirected, errorRedirected, handle =>
        {
            requested.Add(handle);
            return null;
        }, errors);

        Assert.True(supported);
        Assert.Equal(expectedHandles, requested);
        Assert.Equal("", errors.ToString());
    }

    [Theory]
    [InlineData(-11, "stdout")]
    [InlineData(-12, "stderr")]
    public void Initialize_ReturnsFalseAndWarnsOnFailure_WithoutSkippingOtherStream(int failedHandle, string stream)
    {
        var requested = new List<int>();
        using var errors = new StringWriter();

        var supported = WindowsConsole.Initialize(false, false, handle =>
        {
            requested.Add(handle);
            return handle == failedHandle ? 5 : null;
        }, errors);

        Assert.False(supported);
        Assert.Equal(new[] { -11, -12 }, requested);
        Assert.StartsWith($"WARNING: Unable to enable ANSI formatting on {stream} (Windows error 5:", errors.ToString());
        Assert.Contains("Continuing with plain output.", errors.ToString());
        Assert.DoesNotContain('\u001b', errors.ToString());
    }

    [Fact]
    public void Initialize_ReportsBothStreamFailures()
    {
        using var errors = new StringWriter();

        Assert.False(WindowsConsole.Initialize(false, false, _ => 5, errors));

        Assert.Contains("ANSI formatting on stdout", errors.ToString());
        Assert.Contains("ANSI formatting on stderr", errors.ToString());
    }
}
