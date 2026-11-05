// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
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
        var temporary = Directory.CreateTempSubdirectory("azclips publish-assets-").FullName;
        try
        {
            // Prepare exactly the discovered projects together, including extensions not referenced by Cli.Tests.
            var solution = Path.Combine(temporary, "Extensions.slnx");
            new XDocument(new XElement("Solution",
                projects.Select(project => new XElement("Project",
                    new XAttribute("Path", Path.GetRelativePath(temporary, project))))))
                .Save(solution);
            await RunDotNetAsync(repository,
                "build", solution, "--configuration", configuration, "--disable-build-servers");

            foreach (var project in projects)
            {
                var name = Path.GetFileNameWithoutExtension(project)["Extensions.".Length..];
                var assemblyName = $"Microsoft.Azure.Azclips.Extensions.{name}";
                var output = Path.Combine(temporary, name);
                await RunDotNetAsync(repository,
                    "publish", project,
                    "--configuration", configuration, "--output", output,
                    "--no-build", "--no-restore", "--disable-build-servers");
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

    private static async Task RunDotNetAsync(string repository, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
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
            throw new TimeoutException($"dotnet {string.Join(" ", arguments)} did not complete within five minutes.");
        }
        Assert.True(process.ExitCode == 0,
            $"dotnet {string.Join(" ", arguments)} failed:{Environment.NewLine}{await stdout}{await stderr}");
    }
}
