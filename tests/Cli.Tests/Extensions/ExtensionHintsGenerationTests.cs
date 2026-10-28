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
            Assert.True(process.ExitCode == 0, await stdout + await stderr);

            var expectedFile = Path.GetFullPath(
                Path.Combine(projectDirectory, expectedDirectory, "extension-hints.json"));
            Assert.True(File.Exists(expectedFile), $"Expected hints at {expectedFile}. {await stdout}");
            Assert.Equal(new[] { expectedFile },
                Directory.GetFiles(projectDirectory, "extension-hints.json", SearchOption.AllDirectories));
            using var items = JsonDocument.Parse(await stdout);
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
}
