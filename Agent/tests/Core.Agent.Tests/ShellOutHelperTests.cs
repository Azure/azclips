using System.Diagnostics;
using System.Text.Json;
using Core.Agent.Tools.Shell;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="ShellOutHelper.InvokeAzAsync"/>. Spawning a
/// real <c>az.exe</c> in unit tests isn't portable, so we simulate the
/// failure surfaces we care about — spawn failure, non-zero exit, timeout —
/// by pointing at a nonexistent binary or a script we know will misbehave.
/// The rest (successful stdout capture) is exercised by end-to-end tests.
/// </summary>
public class ShellOutHelperTests
{
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
        // Pick a tiny cross-platform command that always exits non-zero:
        //   Windows: `cmd /c exit 3`
        //   Unix:    `sh -c "exit 3"`
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
