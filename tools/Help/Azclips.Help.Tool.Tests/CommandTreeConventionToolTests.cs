// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Azclips.Cli.Export;

namespace Azclips.Help.Tool.Tests;

public sealed class CommandTreeConventionToolTests
{
    [Fact]
    public void EmptyArtifacts_AlsoFailDocsExportWithoutPublishing()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "output");

        var exitCode = Exporter.Run(new ExporterOptions(temp.Path, output, ExtensionFilter: null));

        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task EmptyArtifacts_FailsWithDiagnosticAndDoesNotPublishHelp()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "output");

        var result = await RunToolAsync(temp.Path, output);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Tool_NoExtensionsFound", result.Output);
        Assert.Contains("--artifacts-root", result.Output);
        Assert.Empty(Directory.EnumerateFiles(output, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BuiltExtensions_PassConventionsAndPublishHelp()
    {
        using var temp = new TempDirectory();

        var result = await RunToolAsync(Path.Combine(RepoRoot.Resolve(), "artifacts", Configuration), temp.Path);

        Assert.True(result.ExitCode == 0, result.Output);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp.Path, "HelpIndex.json")));
        var commands = index.RootElement.GetProperty("commands");
        Assert.NotEmpty(commands.EnumerateArray());
    }

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static async Task<(int ExitCode, string Output)> RunToolAsync(string artifacts, string output)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
                 {
                     Path.Combine(RepoRoot.Resolve(), "tools", "Help", "Azclips.Help.Tool", "bin", Configuration, "net10.0", "Azclips.Help.Tool.dll"),
                     "--artifacts-root", artifacts, "--output-root", output,
                 })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        return (process.ExitCode, await stdout + await stderr);
    }
}
