// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for the detached <c>--upload-telemetry</c> spawn shape built by
/// <c>Core.Cli.Program.BuildTelemetryUploaderStartInfo</c>.
/// </summary>
public class TelemetryUploaderSpawnTests
{
    [Theory]
    [InlineData("az")]
    [InlineData("az.exe")]
    public void BuildTelemetryUploaderStartInfo_ShouldRedirectAllStdio_WhenSpawningUploader(string exeName)
    {
        // Built with Path.Combine so the separators are native: the production guard
        // uses Path.GetFileName, which only recognises the running OS's separator.
        var exePath = Path.Combine(AppContext.BaseDirectory, exeName);

        var startInfo = Core.Cli.Program.BuildTelemetryUploaderStartInfo(exePath);

        Assert.NotNull(startInfo);

        // The uploader outlives the CLI. If it inherited our stdio, the write end of
        // the caller's pipe would stay open after `az` exits and a reader such as
        // `az ... | jq` would block until the upload completed. Redirecting all three
        // handles is what keeps a piped invocation as fast as an unpiped one.
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.RedirectStandardInput);

        // Redirection only takes effect when the process is not shell-executed.
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Theory]
    [InlineData("az")]
    [InlineData("az.exe")]
    public void BuildTelemetryUploaderStartInfo_ShouldReenterSameExecutable_WhenSpawningUploader(string exeName)
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, exeName);

        var startInfo = Core.Cli.Program.BuildTelemetryUploaderStartInfo(exePath);

        Assert.NotNull(startInfo);
        Assert.Equal(exePath, startInfo.FileName);
        Assert.Equal("--upload-telemetry", startInfo.Arguments);
    }

    [Theory]
    [InlineData(new[] { "vm", "list", "--profile", "eu-profile" }, "eu-profile")]
    [InlineData(new[] { "vm", "list", "--profile=eu-profile" }, "eu-profile")]
    [InlineData(new[] { "vm", "list", "-profile", "eu-profile" }, "eu-profile")]
    [InlineData(new[] { "vm", "list", "-profile=eu-profile" }, "eu-profile")]
    [InlineData(new[] { "vm", "list", "--profile", "row-profile", "--profile=eu-profile" }, "eu-profile")]
    [InlineData(new[] { "vm", "list", "--profile=eu-profile", "-profile", "row-profile" }, "row-profile")]
    [InlineData(new[] { "vm", "list" }, null)]
    public void TryGetProfileOverride_ReturnsExplicitCliProfile_WhenPresent(string[] args, string? expected)
    {
        Assert.Equal(expected, Core.Cli.Program.TryGetProfileOverride(args));
    }
}
