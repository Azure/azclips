// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class ExtensionPublishAssetsTests
{
    [Fact]
    public async Task Publish_AllExtensionsExcludeHostCoreAssets()
    {
        var repository = RepoLocator.GetRepoRoot();
        var configuration = typeof(ExtensionPublishAssetsTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var projects = Directory.GetDirectories(Path.Combine(repository, "src", "Extensions"))
            .SelectMany(directory => Directory.GetFiles(directory, "Extensions.*.csproj"))
            .OrderBy(project => project, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(projects);
        var temporary = Path.Combine(Path.GetTempPath(), $"azclips-publish-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            foreach (var project in projects)
            {
                var name = Path.GetFileNameWithoutExtension(project)["Extensions.".Length..];
                var assemblyName = $"Microsoft.Azure.Azclips.Extensions.{name}";
                var output = Path.Combine(temporary, name);
                var start = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = repository,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                foreach (var argument in new[]
                {
                    "publish", project,
                    "--configuration", configuration, "--output", output, "--no-build", "--no-restore"
                })
                {
                    start.ArgumentList.Add(argument);
                }
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                try
                {
                    await Task.WhenAll(process.WaitForExitAsync(), stdout, stderr).WaitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                    }
                    throw new TimeoutException($"Publishing {project} did not complete within five minutes.");
                }
                Assert.True(process.ExitCode == 0, await stdout + await stderr);
                Assert.True(File.Exists(Path.Combine(output, $"{assemblyName}.dll")));
                Assert.Empty(Directory.GetFiles(output, "Core.*.dll", SearchOption.AllDirectories));
                Assert.Empty(Directory.GetFiles(output, "Microsoft.Extensions.DependencyInjection*.dll", SearchOption.AllDirectories));
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
