// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Tests.Common.Utilities;

namespace Core.Extensions.Tests;

public sealed class ExtensionSidecarGenerationTests
{
    [Fact]
    public async Task Tool_DoesNotInheritTargetRuntimeSettings()
    {
        var repository = RepoLocator.GetRepoRoot();
        var result = await RunDotNetAsync(repository,
            "msbuild", Path.Combine(repository, "tools", "ExtensionSidecar", "Azclips.ExtensionSidecar.csproj"),
            "-getProperty:RuntimeIdentifier,SelfContained,UseAppHost",
            "-p:RuntimeIdentifier=linux-x64", "-p:SelfContained=true", "-p:UseAppHost=true");
        Assert.True(result.ExitCode == 0, result.Output);
        using var properties = JsonDocument.Parse(result.Output);
        var values = properties.RootElement.GetProperty("Properties");
        Assert.Equal("", values.GetProperty("RuntimeIdentifier").GetString());
        Assert.Equal("false", values.GetProperty("SelfContained").GetString());
        Assert.Equal("false", values.GetProperty("UseAppHost").GetString());
    }

    [Theory]
    [InlineData("SharedAlpha", true)]
    [InlineData("Malformed", false)]
    public async Task Generation_InReusableWorker_ReleasesAssemblies(string fixture, bool valid)
    {
        var repository = RepoLocator.GetRepoRoot();
        var configuration = typeof(ExtensionSidecarGenerationTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var temporary = Directory.CreateTempSubdirectory("azclips-sidecar locks-").FullName;
        try
        {
            var toolDirectory = Path.Combine(temporary, "tools", "ExtensionSidecar");
            var extensionDirectory = Path.Combine(temporary, "extension");
            var coreDirectory = Path.Combine(temporary, "artifacts", configuration, "Core");
            Directory.CreateDirectory(toolDirectory);
            Directory.CreateDirectory(extensionDirectory);
            Directory.CreateDirectory(coreDirectory);
            Directory.CreateDirectory(Path.Combine(temporary, "obj"));

            var builtTool = Path.Combine(repository, "artifacts", configuration, "Tools", "ExtensionSidecar");
            foreach (var file in Directory.GetFiles(builtTool))
            {
                File.Copy(file, Path.Combine(toolDirectory, Path.GetFileName(file)));
            }
            File.Copy(Path.Combine(builtTool, "Core.Extensions.dll"), Path.Combine(coreDirectory, "Core.Extensions.dll"));
            File.Copy(Path.Combine(builtTool, "Core.Abstractions.dll"), Path.Combine(coreDirectory, "Core.Abstractions.dll"));
            var assemblyName = $"Azclips.Tests.Fixtures.{fixture}";
            var targetPath = Path.Combine(extensionDirectory, $"{assemblyName}.dll");
            File.Copy(Path.Combine(repository, "tests", "Fixtures", fixture, "bin", configuration,
                "net10.0", $"{assemblyName}.dll"), targetPath);

            // Resolve the real tool from an isolated copy so lock checks cannot race other tests.
            new XDocument(new XElement("Project",
                new XElement("Target", new XAttribute("Name", "GetTargetPath"),
                    new XAttribute("Returns", Path.Combine(toolDirectory, "Azclips.ExtensionSidecar.dll")))))
                .Save(Path.Combine(toolDirectory, "Azclips.ExtensionSidecar.csproj"));

            var project = Path.Combine(temporary, "Sidecar.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("RepoTools", Path.Combine(temporary, "tools") + Path.DirectorySeparatorChar),
                    new XElement("RepoArtifacts", Path.Combine(temporary, "artifacts") + Path.DirectorySeparatorChar),
                    new XElement("Configuration", configuration),
                    new XElement("TargetPath", targetPath),
                    new XElement("OutputPath", extensionDirectory + Path.DirectorySeparatorChar),
                    new XElement("IntermediateOutputPath", Path.Combine(temporary, "obj") + Path.DirectorySeparatorChar),
                    new XElement("PackageId", assemblyName)),
                new XElement("Import", new XAttribute("Project",
                    Path.Combine(repository, "src", "Extensions", "Targets", "ExtensionSidecar.targets")))))
                .Save(project);

            // The child project runs in a reusable worker, not just the short-lived MSBuild entry process.
            var driver = Path.Combine(temporary, "Driver.proj");
            new XDocument(new XElement("Project",
                new XElement("Target", new XAttribute("Name", "Build"),
                    new XElement("MSBuild", new XAttribute("Projects", project),
                        new XAttribute("Targets", "GenerateExtensionSidecar"),
                        new XAttribute("BuildInParallel", "true")))))
                .Save(driver);

            var result = await BuildAsync(repository, driver, "1.2.3");
            var sidecarPath = Path.Combine(extensionDirectory, $"{assemblyName}.commands.json");
            if (valid)
            {
                Assert.True(result.ExitCode == 0, result.Output);
                using var sidecar = JsonDocument.Parse(await File.ReadAllTextAsync(sidecarPath));
                Assert.Equal(1, sidecar.RootElement.GetProperty("schemaVersion").GetInt32());
                Assert.Equal(assemblyName, sidecar.RootElement.GetProperty("packageId").GetString());
                Assert.Equal("1.2.3", sidecar.RootElement.GetProperty("version").GetString());
                Assert.Equal(["sharedverb"], sidecar.RootElement.GetProperty("verbs")
                    .EnumerateArray().Select(value => value.GetString()));
                Assert.Equal(["sharedverb alpha"], sidecar.RootElement.GetProperty("leaves")
                    .EnumerateArray().Select(value => value.GetString()));

                var originalContent = await File.ReadAllTextAsync(sidecarPath);
                var originalTimestamp = File.GetLastWriteTimeUtc(sidecarPath);
                result = await BuildAsync(repository, driver, "1.2.3");
                Assert.True(result.ExitCode == 0, result.Output);
                Assert.Equal(originalTimestamp, File.GetLastWriteTimeUtc(sidecarPath));
                Assert.Equal(originalContent, await File.ReadAllTextAsync(sidecarPath));

                result = await BuildAsync(repository, driver, "2.0.0");
                Assert.True(result.ExitCode == 0, result.Output);
                using var updated = JsonDocument.Parse(await File.ReadAllTextAsync(sidecarPath));
                Assert.Equal("2.0.0", updated.RootElement.GetProperty("version").GetString());
            }
            else
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("does not implement IExtensionIndex", result.Output);
                Assert.False(File.Exists(sidecarPath));
            }

            foreach (var dll in Directory.GetFiles(temporary, "*.dll", SearchOption.AllDirectories))
            {
                using var exclusive = new FileStream(dll, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static Task<(int ExitCode, string Output)> BuildAsync(
        string repository, string project, string version) =>
        RunDotNetAsync(repository,
            "msbuild", project, "-t:Build", "-m:2", "-nr:true", "-nologo", $"-p:PackageVersion={version}");

    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string repository, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(), stdout, stderr).WaitAsync(TimeSpan.FromMinutes(1));
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            throw;
        }
        return (process.ExitCode, await stdout + await stderr);
    }
}
