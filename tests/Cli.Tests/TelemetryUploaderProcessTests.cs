// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Cli.Tests;

/// <summary>
/// Regression tests for the detached telemetry uploader's process start info.
/// See Azure/azclips#727: an unredirected child process inherits the parent's
/// stdio file descriptors. When `az`'s stdout is itself a pipe (e.g.
/// `az ... | cat`), an inherited-but-unredirected child keeps that pipe's
/// write end open until it exits — even after `az` has already returned —
/// so the downstream reader blocks for as long as the background upload takes.
/// </summary>
public class TelemetryUploaderProcessTests
{
    [Fact]
    public void BuildTelemetryUploaderStartInfo_RedirectsAllStdioStreams()
    {
        var startInfo = Core.Cli.Program.BuildTelemetryUploaderStartInfo("/path/to/az");

        Assert.True(startInfo.RedirectStandardOutput, "stdout must be redirected so the child doesn't inherit the parent's stdout pipe.");
        Assert.True(startInfo.RedirectStandardError, "stderr must be redirected so the child doesn't inherit the parent's stderr pipe.");
        Assert.True(startInfo.RedirectStandardInput, "stdin must be redirected so the child doesn't inherit the parent's stdin.");
    }

    [Fact]
    public void BuildTelemetryUploaderStartInfo_TargetsUploadTelemetryArgument()
    {
        var startInfo = Core.Cli.Program.BuildTelemetryUploaderStartInfo("/path/to/az");

        Assert.Equal("/path/to/az", startInfo.FileName);
        Assert.Equal("--upload-telemetry", startInfo.Arguments);
        Assert.False(startInfo.UseShellExecute);
    }
}
