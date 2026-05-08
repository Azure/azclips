using System.Text.Json;
using Core.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Login;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class HelpArtifactWriterTests
{
    [Fact]
    public void WriteArtifacts_ShouldMatchGoldenFiles_ForLoginExtension()
    {
        var index = new ExtensionIndex();
        var repoRoot = RepoLocator.GetRepoRoot();
        var examplesPath = Path.Combine(repoRoot, "src", "Extensions", "Login", "Processors");

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0-alpha", examplesPath);
        var helpIndex = ExtensionHelpManifestBuilder.BuildIndex([manifest]);

        using var output = new TempOutputFixture();
        HelpArtifactWriter.WriteArtifacts(output.DirectoryPath, manifest, helpIndex);

        var generatedManifestPath = Path.Combine(output.DirectoryPath, "HelpManifest.json");
        var generatedIndexPath = Path.Combine(output.DirectoryPath, "HelpIndex.json");

        var goldenManifestPath = Path.Combine(repoRoot, "tests", "Cli.Tests", "Golden", "Help", "Login.HelpManifest.json");
        var goldenIndexPath = Path.Combine(repoRoot, "tests", "Cli.Tests", "Golden", "Help", "Login.HelpIndex.json");

        AssertJsonEquivalent(goldenManifestPath, generatedManifestPath);
        AssertJsonEquivalent(goldenIndexPath, generatedIndexPath);
    }

    private static void AssertJsonEquivalent(string expectedPath, string actualPath)
    {
        var expectedJson = File.ReadAllText(expectedPath);
        var actualJson = File.ReadAllText(actualPath);

        using var expectedDoc = JsonDocument.Parse(expectedJson);
        using var actualDoc = JsonDocument.Parse(actualJson);

        Assert.True(JsonElement.DeepEquals(expectedDoc.RootElement, actualDoc.RootElement),
            $"JSON mismatch. Expected: {expectedPath}. Actual: {actualPath}.");
    }

    private sealed class TempOutputFixture : IDisposable
    {
        public TempOutputFixture()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "cirrus-help-artifacts-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
