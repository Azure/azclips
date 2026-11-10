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
    public void WriteArtifacts_ShouldMatchFixtures_ForSyntheticManifest()
    {
        var repoRoot = RepoLocator.GetRepoRoot();
        var manifest = new HelpManifest("sample", "1.0.0-test", "Sample commands.",
        [
            new HelpCommand(
                CliName: "sample item create",
                PsName: "New-SampleItem",
                Summary: "Create a sample item.",
                Description: "A stable description.",
                Parameters:
                [
                    new HelpParameter("name", "Name", "string", true, "The item name.",
                        ["n"], ["first", "second"], "Default", false, false,
                        "Item Arguments", "source")
                ],
                Examples: new HelpExampleSet(
                    [new HelpExample("Create an item", "az sample item create --name first\naz sample item show --name first")],
                    [new HelpExample("Create an item", "New-SampleItem -Name first")]),
                Tags: [],
                PreviewMessage: "Preview message.")
        ]);
        var helpIndex = ExtensionHelpManifestBuilder.BuildIndex([manifest]);

        using var output = new TempOutputFixture();
        HelpArtifactWriter.WriteArtifacts(output.DirectoryPath, manifest, helpIndex);

        var generatedManifestPath = Path.Combine(output.DirectoryPath, "HelpManifest.json");
        var generatedIndexPath = Path.Combine(output.DirectoryPath, "HelpIndex.json");

        var expectedManifestPath = Path.Combine(repoRoot, "tests", "Cli.Tests", "Fixtures", "Help", "Synthetic.HelpManifest.json");
        var expectedIndexPath = Path.Combine(repoRoot, "tests", "Cli.Tests", "Fixtures", "Help", "Synthetic.HelpIndex.json");

        AssertJsonEquivalent(expectedManifestPath, generatedManifestPath);
        AssertJsonEquivalent(expectedIndexPath, generatedIndexPath);
    }

    [Fact]
    public async Task WriteCatalog_ConcurrentWritersPublishWholeDocuments()
    {
        using var output = new TempOutputFixture();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(new ExtensionIndex(), "1.0.0", "login");
        var index = ExtensionHelpManifestBuilder.BuildIndex([manifest]);
        var fingerprints = Enumerable.Range(0, 8).Select(i => $"writer-{i}").ToArray();

        await Task.WhenAll(fingerprints.Select(fingerprint => Task.Run(() =>
            HelpArtifactWriter.WriteCatalog(output.DirectoryPath, new HelpCatalogHeader(1, "test", fingerprint), [manifest], index))));

        var published = new List<string?>();
        foreach (var name in new[] { "HelpManifest.json", "HelpIndex.json" })
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output.DirectoryPath, name)));
            Assert.Contains(document.RootElement.GetProperty("header").GetProperty("extensionFingerprint").GetString(), fingerprints);
            published.Add(document.RootElement.GetProperty("header").GetProperty("extensionFingerprint").GetString());
            Assert.Equal(1, document.RootElement.GetProperty("header").GetProperty("schemaVersion").GetInt32());
            if (name == "HelpManifest.json")
                Assert.Equal("login", document.RootElement.GetProperty("manifests")[0].GetProperty("commands")[0].GetProperty("cliName").GetString());
            else
                Assert.Equal("login", document.RootElement.GetProperty("index").GetProperty("commands")[0].GetProperty("cliName").GetString());
        }
        Assert.Equal(published[0], published[1]);
        Assert.Empty(Directory.GetFiles(output.DirectoryPath, "*.pending"));
    }

    [Fact]
    public void WriteCatalog_PreCancelledDoesNotPublishArtifacts()
    {
        using var output = new TempOutputFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => HelpArtifactWriter.WriteCatalog(
            output.DirectoryPath, new HelpCatalogHeader(1, "test", "stamp"), [], new([], []), cts.Token));
        Assert.Empty(Directory.GetFiles(output.DirectoryPath));
    }

    [Fact]
    public void WriteCatalog_CancellationWhileWaitingForAnotherPublisher_DoesNotPublish()
    {
        using var output = new TempOutputFixture();
        using var lease = new FileStream(Path.Combine(output.DirectoryPath, HelpArtifactWriter.CatalogLockFileName),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Assert.ThrowsAny<OperationCanceledException>(() => HelpArtifactWriter.WriteCatalog(
            output.DirectoryPath, new HelpCatalogHeader(1, "test", "stamp"), [], new([], []), cts.Token));
        Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "HelpManifest.json")));
        Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "HelpIndex.json")));
    }

    [Fact]
    public void WriteCatalog_ReplacesFilesHeldOpenByReaders()
    {
        using var output = new TempOutputFixture();
        HelpArtifactWriter.WriteCatalog(output.DirectoryPath, new HelpCatalogHeader(1, "test", "old"), [], new([], []));
        var paths = new[] { "HelpManifest.json", "HelpIndex.json" }.Select(name => Path.Combine(output.DirectoryPath, name)).ToArray();

        using (var manifestReader = new FileStream(paths[0], FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        using (var indexReader = new FileStream(paths[1], FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            HelpArtifactWriter.WriteCatalog(output.DirectoryPath, new HelpCatalogHeader(1, "test", "new"), [], new([], []));
        }

        foreach (var path in paths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("new", document.RootElement.GetProperty("header").GetProperty("extensionFingerprint").GetString());
        }
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
