// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azclips.Cli.Export;
using Core.Cli.Help;

namespace Azclips.Help.Tool.Tests;

/// <summary>
/// Runs the exporter once against the built extension DLLs for integration assertions.
/// Shared via <see cref="IClassFixture{T}"/> so we only
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

    public void Dispose() => _temp.Dispose();
}

public class CliExportTests : IClassFixture<CliExportFixture>
{
    private readonly CliExportFixture _fixture;

    public CliExportTests(CliExportFixture fixture) => _fixture = fixture;

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

    [Fact]
    public void RootExport_HasCli2YamlGroupShape()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_fixture.OutputLatest, "_root_.json")));
        var root = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("", root.GetProperty("command").GetString());
        Assert.Equal("group", root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("short_summary").GetString()));
        Assert.Equal("", root.GetProperty("long_summary").GetString());
        Assert.False(root.TryGetProperty("command_source", out _));
        Assert.False(root.TryGetProperty("examples", out _));
        Assert.False(root.TryGetProperty("parameters", out _));
    }

    [Theory]
    [InlineData("login", "login", "--subscription-id")]
    [InlineData("vm", "vm create", "--name")]
    public void CommandExport_PreservesAuthoredHelpAndParameters(string verb, string commandName, string parameterName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_fixture.OutputLatest, $"{verb}.json")));
        var entries = document.RootElement.EnumerateArray().ToArray();
        var command = Assert.Single(entries, entry =>
            entry.GetProperty("type").GetString() == "command"
            && entry.GetProperty("command").GetString() == commandName);
        var commandKey = commandName == verb ? "" : commandName[(verb.Length + 1)..];
        var markdown = MarkdownCommandHelpProvider.Get(verb, commandKey);

        Assert.False(string.IsNullOrWhiteSpace(markdown.Summary));
        Assert.Equal(markdown.Summary, command.GetProperty("short_summary").GetString());
        Assert.Equal(markdown.Description, command.GetProperty("long_summary").GetString());

        var expectedExamples = markdown.Examples.Where(example => !example.IsPowerShell).ToArray();
        Assert.NotEmpty(expectedExamples);
        var actualExamples = command.GetProperty("examples").EnumerateArray().ToArray();
        Assert.Equal(expectedExamples.Length, actualExamples.Length);
        for (var i = 0; i < expectedExamples.Length; i++)
        {
            Assert.Equal(expectedExamples[i].Title, actualExamples[i].GetProperty("short_summary").GetString());
            Assert.Equal(expectedExamples[i].Command, actualExamples[i].GetProperty("command").GetString());
        }

        var parameters = command.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Single(parameters, parameter => parameter.GetProperty("name").GetString()!.Split(' ')[0] == parameterName);
        Assert.Contains(parameters, parameter => parameter.GetProperty("name").GetString()!.Split(' ')[0] == "--help"
            && parameter.GetProperty("group_name").GetString() == "Global Arguments");
        Assert.All(parameters, parameter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.GetProperty("name").GetString()));
            Assert.Equal(JsonValueKind.String, parameter.GetProperty("short_summary").ValueKind);
            Assert.True(parameter.GetProperty("required").ValueKind is JsonValueKind.True or JsonValueKind.False);
        });
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
