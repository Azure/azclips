// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class ExtensionHintsGenerationTests
{
    [Theory]
    [InlineData(@"obj\Debug/", "obj/Debug")]
    [InlineData(@"obj\Release/", "obj/Release")]
    [InlineData("custom/intermediate/", "custom/intermediate")]
    public async Task Generate_WritesHintsToNormalizedIntermediateDirectory(
        string intermediateOutputPath, string expectedDirectory)
    {
        var repository = RepoLocator.GetRepoRoot();
        var temporary = Path.Combine(Path.GetTempPath(), $"azclips-hints-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(temporary, "src", "CLI");
        var extensionDirectory = Path.Combine(temporary, "src", "Extensions", "Example");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(extensionDirectory);
        try
        {
            var targets = Path.Combine(repository, "src", "CLI", "Targets", "ExtensionHints.targets");
            var project = Path.Combine(projectDirectory, "Hints.proj");
            var document = new XDocument(
                new XElement("Project",
                    new XElement("PropertyGroup",
                        new XElement("IntermediateOutputPath", intermediateOutputPath)),
                    new XElement("Import", new XAttribute("Project", targets))));
            document.Save(project);
            await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "Example.csproj"),
                "<Project><PropertyGroup><PackageId>Example.Extension</PackageId></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "ExtensionIndex.cs"),
                """[ExtensionVerb("example")] public class ExtensionIndex { }""");

            var (exit, stdout, stderr) = await RunGenerationAsync(repository, project);
            Assert.True(exit == 0, stdout + stderr);

            var expectedFile = Path.GetFullPath(
                Path.Combine(projectDirectory, expectedDirectory, "extension-hints.json"));
            Assert.True(File.Exists(expectedFile), $"Expected hints at {expectedFile}. {stdout}");
            Assert.Equal(new[] { expectedFile },
                Directory.GetFiles(projectDirectory, "extension-hints.json", SearchOption.AllDirectories));
            using var items = JsonDocument.Parse(stdout);
            var resource = Assert.Single(items.RootElement.GetProperty("Items")
                .GetProperty("EmbeddedResource").EnumerateArray());
            Assert.Equal(expectedFile, resource.GetProperty("FullPath").GetString());
            Assert.Equal("extension-hints.json", resource.GetProperty("LogicalName").GetString());
            using var hints = JsonDocument.Parse(await File.ReadAllTextAsync(expectedFile));
            Assert.Equal("Example.Extension",
                hints.RootElement.GetProperty("leaves").GetProperty("example").GetProperty("packageId").GetString());
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [Theory]
    [InlineData("blob", "account list", true)]
    [InlineData("", "account list", true)]
    [InlineData("blob", "blob", false)]
    [InlineData("", "", false)]
    public async Task Generate_AllowsSharedNamespaces_ButRejectsDuplicateCommandLeaves(
        string firstPath, string secondPath, bool succeeds)
    {
        var repository = RepoLocator.GetRepoRoot();
        var temporary = Directory.CreateTempSubdirectory("azclips-shared-hints-").FullName;
        try
        {
            var projectDirectory = Path.Combine(temporary, "src", "CLI");
            Directory.CreateDirectory(projectDirectory);
            var project = Path.Combine(projectDirectory, "Hints.proj");
            var targets = Path.Combine(repository, "src", "CLI", "Targets", "ExtensionHints.targets");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("IntermediateOutputPath", @"obj\Debug\")),
                new XElement("Import", new XAttribute("Project", targets)))).Save(project);

            var paths = new[] { firstPath, secondPath };
            for (var i = 0; i < paths.Length; i++)
            {
                var extensionDirectory = Path.Combine(temporary, "src", "Extensions", $"Example{i}");
                Directory.CreateDirectory(extensionDirectory);
                await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "Example.csproj"),
                    $"<Project><PropertyGroup><PackageId>Example{i}.Extension</PackageId></PropertyGroup></Project>");
                var invocation = string.IsNullOrEmpty(paths[i]) ? "storage" : $"storage {paths[i]}";
                await File.WriteAllTextAsync(Path.Combine(extensionDirectory, "ExtensionIndex.cs"),
                    $$"""
                    [ExtensionVerb("storage")]
                    public class ExtensionIndex
                    {
                        private static readonly Dictionary<string, object> s_cliTable = new()
                        {
                            ["{{invocation}}"] = null,
                        };
                    }
                    """);
            }

            var (exit, stdout, stderr) = await RunGenerationAsync(repository, project);
            if (!succeeds)
            {
                Assert.NotEqual(0, exit);
                var duplicate = string.IsNullOrEmpty(firstPath) ? "storage" : $"storage {firstPath}";
                Assert.Contains($"'{duplicate}' claimed by both", stdout + stderr);
                return;
            }

            Assert.True(exit == 0, stdout + stderr);
            using var hints = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(projectDirectory, "obj", "Debug", "extension-hints.json")));
            var leaves = hints.RootElement.GetProperty("leaves");
            Assert.Equal(string.IsNullOrEmpty(firstPath), leaves.TryGetProperty("storage", out _));
            for (var i = 0; i < paths.Length; i++)
            {
                var invocation = string.IsNullOrEmpty(paths[i]) ? "storage" : $"storage {paths[i]}";
                Assert.Equal($"Example{i}.Extension",
                    leaves.GetProperty(invocation).GetProperty("packageId").GetString());
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunGenerationAsync(
        string repository, string project)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
        {
            "msbuild", project, "-t:GenerateExtensionHints", "-getItem:EmbeddedResource", "-nologo"
        })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(), stdout, stderr)
                .WaitAsync(TimeSpan.FromMinutes(1));
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
        return (process.ExitCode, await stdout, await stderr);
    }
}
