using System.Text.Json;
using Core.Agent.Tools.Shell;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="ShellOutHelper.InvokeAzAsync"/>. Uses a
/// tiny cross-platform shell (cmd.exe on Windows, /bin/sh elsewhere) to
/// exercise the caller-facing surfaces: stdout on success, and three of the
/// structured-JSON error shapes — spawn-failed, non-zero exit, and caller
/// cancellation. (The timeout shape isn't covered here; it would need the
/// timeout to be configurable to exercise deterministically.)
/// </summary>
public class ShellOutHelperTests
{
    [Fact]
    public async Task SuccessfulExit_ReturnsStdout()
    {
        // A real subprocess whose stdout must reach the caller — this is how
        // LLM tool results flow to the model, so a regression that drops output
        // entirely (spawn/redirect/buffer break) shows up here. Note stdout is
        // line-collected (OutputDataReceived + AppendLine), so it is not a
        // byte-for-byte copy of the child's stream; the content is what matters.
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

        var result = await ShellOutHelper.InvokeAzAsync(bogus, new[] { "agent", "tool", "env", "show" });

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

    [Fact]
    public async Task CallerCancellation_ReturnsCancelledJson()
    {
        // A child that would run far longer than the caller wants; the caller's
        // token is already cancelled, so InvokeAzAsync must kill the child and
        // return the shell_out_cancelled shape rather than throwing.
        var (exe, args) = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", new[] { "/c", "ping", "-n", "30", "127.0.0.1" })
            : ("/bin/sh", new[] { "-c", "sleep 30" });

        if (!File.Exists(exe))
            return; // env doesn't provide a shell we can reach — skip

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ShellOutHelper.InvokeAzAsync(exe, args, cts.Token);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("shell_out_cancelled", doc.RootElement.GetProperty("error").GetString());
    }
}
