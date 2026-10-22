// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Runtime;

namespace Cli.Tests;

/// <summary>
/// Unit tests for <see cref="ConsoleSink"/> — the single owner of console rendering
/// for diagnostic output. Locks the canonical <c>LEVEL: message</c> prefix format and
/// the stderr-vs-stdout routing that <see cref="StreamRoutingIntegrationTests"/> covers
/// at the subprocess level.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ConsoleSinkTests
{
    [Fact]
    public void Error_EmitsErrorPrefixToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.Error("something bad"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("ERROR: something bad", stderr);
    }

    [Fact]
    public void Warn_EmitsWarningPrefixToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.Warn("something noteworthy"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("WARNING: something noteworthy", stderr);
    }

    [Fact]
    public void Info_EmitsInfoPrefixToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.Info("something informational"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("INFO: something informational", stderr);
    }

    [Fact]
    public void Verbose_EmitsVerbosePrefixToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.Verbose("chatty log"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("VERBOSE: chatty log", stderr);
    }

    [Fact]
    public void Debug_EmitsDebugPrefixToStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.Debug("deep diagnostic"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("DEBUG: deep diagnostic", stderr);
    }

    [Fact]
    public void EmitStderr_WritesToStderr_WithoutPrefix()
    {
        // Interactive callers (e.g. ConsoleOutputAdapter.WriteWarningAsync) use the low-level
        // primitive to bypass the LEVEL: prefix while still routing to stderr.
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.EmitStderr(ConsoleColor.Yellow, "raw message"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("raw message", stderr);
        Assert.DoesNotContain("WARNING:", stderr);
    }

    [Fact]
    public void EmitStderrPlain_WritesToStderr_WithoutPrefixOrColor()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.EmitStderrPlain("bare line"));

        Assert.Equal(string.Empty, stdout);
        Assert.Contains("bare line", stderr);
    }

    [Fact]
    public void EmitStdout_WritesToStdout_NotStderr()
    {
        var (stdout, stderr) = CaptureConsole(() => ConsoleSink.EmitStdout(ConsoleColor.Cyan, "stdout text"));

        Assert.Equal(string.Empty, stderr);
        Assert.Contains("stdout text", stdout);
    }

    [Fact]
    public void EmitStdout_WithNewline_AppendsNewline()
    {
        var (stdout, _) = CaptureConsole(() => ConsoleSink.EmitStdout(ConsoleColor.Cyan, "line", newline: true));

        Assert.EndsWith(Environment.NewLine, stdout);
    }

    private static (string stdout, string stderr) CaptureConsole(Action action)
    {
        var origOut = Console.Out;
        var origError = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            action();
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origError);
        }
        return (outWriter.ToString(), errWriter.ToString());
    }
}
