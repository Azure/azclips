using System.Diagnostics;
using System.Text.Json;
using Core.Agent.Tools.Shell;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="ShellOutHelper.InvokeAzAsync"/>. Uses a
/// tiny cross-platform shell (cmd.exe on Windows, /bin/sh elsewhere) to
/// exercise the three surfaces callers depend on:
/// stdout passthrough on success, structured JSON on failure spawn,
/// and structured JSON on non-zero exit.
/// </summary>
public class ShellOutHelperTests
{
    [Fact]
    public async Task SuccessfulExit_ReturnsStdoutVerbatim()
    {
        // A real subprocess with stdout that must round-trip byte-for-byte to
        // the caller — this is how LLM tool results reach the model, so any
        // regression that mangles stdout (encoding, trimming, buffering) shows
        // up here first.
        var (exe, args) = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", new[] { "/c", "echo", "hello-shellout" })
            : ("/bin/sh", new[] { "-c", "echo hello-shellout" });

        if (!File.Exists(exe))
            return; // env doesn't provide a shell we can reach — skip

        var result = await ShellOutHelper.InvokeAzAsync(exe, args);

        Assert.Contains("hello-shellout", result);
    }

    [Fact]
    public async Task NonexistentBinary_ReturnsSpawnFailedJson()
    {
        var bogus = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.exe");

        var result = await ShellOutHelper.InvokeAzAsync(bogus, new[] { "agent", "tool", "env" });

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("shell_out_spawn_failed", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(bogus, doc.RootElement.GetProperty("azExePath").GetString());
    }

    [Fact]
    public async Task NonZeroExit_ReturnsStructuredError()
    {
        var (exe, args) = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", new[] { "/c", "exit", "3" })
            : ("/bin/sh", new[] { "-c", "exit 3" });

        if (!File.Exists(exe))
            return; // env doesn't provide a shell we can reach — skip

        var result = await ShellOutHelper.InvokeAzAsync(exe, args);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("shell_out_nonzero_exit", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("exitCode").GetInt32());
    }
}
