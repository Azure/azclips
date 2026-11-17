// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azclips.Cli.Export;

namespace Azclips.Help.Tool.Tests;

/// <summary>
/// Runs the exporter once against the built extension DLLs and lets tests diff each verb's
/// JSON against the checked-in fixture. Shared via <see cref="IClassFixture{T}"/> so we only
/// pay the assembly-load + reflection cost once per test class.
/// </summary>
public sealed class CliExportFixture : IDisposable
{
    private readonly TempDirectory _temp;

    public CliExportFixture()
    {
        var repoRoot = RepoRoot.Resolve();
        var configuration = ResolveBuildConfiguration();
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", configuration);

        if (!Directory.Exists(artifactsRoot))
        {
            throw new InvalidOperationException(
                $"Artifacts root not found at {artifactsRoot}. Run 'dotnet build azclips.slnx' before these tests.");
        }

        _temp = new TempDirectory();

        var exitCode = Exporter.Run(new ExporterOptions(
            ArtifactsRoot: artifactsRoot,
            OutputFolder: _temp.Path,
            ExtensionFilter: null));

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Exporter exited with code {exitCode}.");
        }

        OutputLatest = Path.Combine(_temp.Path, "latest");
        FixtureRoot = Path.Combine(repoRoot,
            "tools", "Help", "Azclips.Help.Tool.Tests", "Fixtures", "cli-export");
    }

    // Locate the build configuration from the test assembly's own path (bin/<Config>/<TFM>/),
    // so the test picks the same artifacts root the CI job just produced.
    private static string ResolveBuildConfiguration()
    {
        var baseDir = AppContext.BaseDirectory;
        var sep = Path.DirectorySeparatorChar;
        if (baseDir.Contains($"{sep}Release{sep}", StringComparison.OrdinalIgnoreCase))
        {
            return "Release";
        }
        return "Debug";
    }

    public string OutputLatest { get; }

    public string FixtureRoot { get; }

    public void Dispose() => _temp.Dispose();
}

public class CliExportFixturesTests : IClassFixture<CliExportFixture>
{
    private readonly CliExportFixture _fixture;

    public CliExportFixturesTests(CliExportFixture fixture) => _fixture = fixture;

    [Fact]
    public void AgentExport_ContainsUniqueGroupsAndAllDocsActions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_fixture.OutputLatest, "agent.json")));
        var entries = document.RootElement.EnumerateArray().ToArray();
        var names = entries.Select(entry => entry.GetProperty("command").GetString()).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var group in new[] { "agent tool", "agent tool docs", "agent tool env", "agent tool resource" })
            Assert.Equal("group", Assert.Single(entries, entry => entry.GetProperty("command").GetString() == group).GetProperty("type").GetString());
        foreach (var command in new[] { "agent tool docs list", "agent tool docs show", "agent tool docs generate", "agent tool env show" })
            Assert.Equal("command", Assert.Single(entries, entry => entry.GetProperty("command").GetString() == command).GetProperty("type").GetString());
        Assert.DoesNotContain(names, name => name == "agent tool help" || name?.StartsWith("agent tool help ", StringComparison.Ordinal) == true);
        var show = Assert.Single(entries, entry => entry.GetProperty("command").GetString() == "agent tool docs show");
        var nameParameter = Assert.Single(show.GetProperty("parameters").EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "--name");
        Assert.True(nameParameter.GetProperty("required").GetBoolean());
    }

    [Theory]
    [InlineData("_root_")]
    [InlineData("login")]
    [InlineData("vm")]
    public void ExporterOutput_MatchesFixture(string verb)
    {
        var fixturePath = Path.Combine(_fixture.FixtureRoot, $"{verb}.json");
        var actualPath = Path.Combine(_fixture.OutputLatest, $"{verb}.json");

        if (!File.Exists(fixturePath))
        {
            Assert.Fail($"Missing fixture '{fixturePath}'. Add or regenerate: pwsh build/Update-CliExportFixtures.ps1");
        }
        if (!File.Exists(actualPath))
        {
            Assert.Fail($"Exporter did not produce '{actualPath}' — the tracked fixture may reference a verb the exporter no longer emits.");
        }

        AssertJsonEquivalent(fixturePath, actualPath);
    }

    private static void AssertJsonEquivalent(string expectedPath, string actualPath)
    {
        var expectedJson = File.ReadAllText(expectedPath);
        var actualJson = File.ReadAllText(actualPath);

        using var expectedDoc = JsonDocument.Parse(expectedJson);
        using var actualDoc = JsonDocument.Parse(actualJson);

        Assert.True(
            JsonElement.DeepEquals(expectedDoc.RootElement, actualDoc.RootElement),
            $"CLI export JSON drift on '{Path.GetFileName(expectedPath)}'. " +
            $"If intentional, refresh the fixture: pwsh build/Update-CliExportFixtures.ps1");
    }
}

internal static class RepoRoot
{
    public static string Resolve()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "azclips.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (azclips.slnx) from " + AppContext.BaseDirectory);
    }
}
