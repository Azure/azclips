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
    [InlineData("/usr/local/bin/az")]
    [InlineData(@"C:\Program Files\azclips\az.exe")]
    public void BuildTelemetryUploaderStartInfo_ShouldRedirectAllStdio_WhenSpawningUploader(string exePath)
    {
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
    [InlineData("/usr/local/bin/az")]
    [InlineData(@"C:\Program Files\azclips\az.exe")]
    public void BuildTelemetryUploaderStartInfo_ShouldReenterSameExecutable_WhenSpawningUploader(string exePath)
    {
        var startInfo = Core.Cli.Program.BuildTelemetryUploaderStartInfo(exePath);

        Assert.NotNull(startInfo);
        Assert.Equal(exePath, startInfo.FileName);
        Assert.Equal("--upload-telemetry", startInfo.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/repo/artifacts/Debug/Benchmark/Benchmark")]
    [InlineData(@"C:\repo\artifacts\Debug\Benchmark\Benchmark.exe")]
    [InlineData("/usr/bin/dotnet")]
    public void BuildTelemetryUploaderStartInfo_ShouldReturnNull_WhenHostIsNotTheAzExecutable(string? exePath)
    {
        Assert.Null(Core.Cli.Program.BuildTelemetryUploaderStartInfo(exePath));
    }
}
