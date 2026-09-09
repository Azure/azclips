// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Cli.Help;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class HelpArtifactWriterTests
{
    [Fact]
    public void WriteArtifacts_ShouldMatchFixtures_ForLoginExtension()
    {
        var index = new ExtensionIndex();
        var repoRoot = RepoLocator.GetRepoRoot();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0-alpha", "login");
        var helpIndex = ExtensionHelpManifestBuilder.BuildIndex([manifest]);

        using var output = new TempOutputFixture();
        HelpArtifactWriter.WriteArtifacts(output.DirectoryPath, manifest, helpIndex);

        var generatedManifestPath = Path.Combine(output.DirectoryPath, "HelpManifest.json");
        var generatedIndexPath = Path.Combine(output.DirectoryPath, "HelpIndex.json");

        var expectedManifestPath = Path.Combine(repoRoot, "tests", "Cli.Tests", "Fixtures", "Help", "Login.HelpManifest.json");
        var expectedIndexPath = Path.Combine(repoRoot, "tests", "Cli.Tests", "Fixtures", "Help", "Login.HelpIndex.json");

        AssertJsonEquivalent(expectedManifestPath, generatedManifestPath);
        AssertJsonEquivalent(expectedIndexPath, generatedIndexPath);
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
            DirectoryPath = Path.Combine(Path.GetTempPath(), "azclips-help-artifacts-tests", Guid.NewGuid().ToString("N"));
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

