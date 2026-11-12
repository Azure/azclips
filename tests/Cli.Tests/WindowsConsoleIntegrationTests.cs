// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;

namespace Cli.Tests;

public class WindowsConsoleIntegrationTests
{
    [WindowsConsoleTheory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    public async Task Help_EnablesNativeAnsiAndRendersBoldWithoutLeakingFormatting(int initialMode)
    {
        var root = Path.GetDirectoryName(typeof(WindowsConsoleIntegrationTests).Assembly.Location);
        while (root is not null && !File.Exists(Path.Combine(root, "azclips.slnx")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var configuration = typeof(WindowsConsoleIntegrationTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var worker = Path.Combine(root, "tests", "Fixtures", "WindowsConsoleWorker", "bin",
            configuration, "net10.0", "WindowsConsole.TestWorker.dll");
        var cli = Path.Combine(root, "artifacts", configuration, "Core.Cli", "az.exe");
        Assert.True(File.Exists(worker), $"Build the Windows console worker first: {worker}");
        Assert.True(File.Exists(cli), $"Build the CLI first: {cli}");

        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = root,
        };
        foreach (var argument in new[] { worker, cli, initialMode.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            start.ArgumentList.Add(argument);

        var home = Directory.CreateTempSubdirectory("azclips-console-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(home.FullName, HomePaths.ConfigFileName),
                """{"telemetry":{"enabled":false}}""");
            start.Environment[HomePaths.HomeOverrideVariable] = home.FullName;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await output;
                var stderr = await error;
                Assert.True(process.ExitCode == 0, $"Console worker exit {process.ExitCode}:\n{stderr}\n{stdout}");
                Assert.Contains("PASS: VT enabled", stdout);
                Assert.Equal("", stderr);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(output, error);
            }
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    private sealed class WindowsConsoleTheoryAttribute : TheoryAttribute
    {
        public WindowsConsoleTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Requires a native Windows console.";
        }
    }
}
