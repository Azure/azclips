using System.Runtime.CompilerServices;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class CSharpClientGeneratorTests
{
    private static IRDocument Ir(IEnumerable<string>? models = null, IEnumerable<string>? operations = null)
    {
        return new IRDocument
        {
            SchemaVersion = "0.6.0",
            Models = (models ?? [])
                .Select(name => new IRModel
                {
                    Name = name,
                    Source = "Test",
                    Id = new IRId { Module = "Test", Name = name },
                })
                .ToList(),
            Operations = (operations ?? [])
                .Select(name => new IROperation { Name = name })
                .ToList(),
        };
    }

    /// <summary>
    /// Run the generator, writing every emitted file to this test's own
    /// <c>artifacts/&lt;test&gt;/</c> folder (for eyeballing) and mirroring them in
    /// memory (for assertions). The scenario defaults to the calling test's name.
    /// </summary>
    private static async Task<(GenerationResult Result, ArtifactWriter Writer)> RunAsync(
        IRDocument ir,
        GeneratorOptions? options = null,
        [CallerMemberName] string scenario = "default")
    {
        var writer = new ArtifactWriter(scenario);
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            options ?? new GeneratorOptions { RootNamespace = "Contoso.Clients" },
            writer.WriteAsync);
        return (result, writer);
    }

    [Fact]
    public async Task Generate_EmitsAModelFilePerModel()
    {
        (GenerationResult result, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget", "Gadget"]));

        Assert.True(result.Success);
        Assert.Contains("Models/Widget.g.cs", writer.Files.Keys);
        Assert.Contains("Models/Gadget.g.cs", writer.Files.Keys);
    }

    [Fact]
    public async Task Generate_EmitsAProcessorFilePerOperation()
    {
        // Operation names are camelCase in the IR; processors are PascalCased.
        (GenerationResult result, ArtifactWriter writer) = await RunAsync(Ir(operations: ["restoreVolume"]));

        Assert.True(result.Success);
        Assert.Contains("Processors/RestoreVolumeProcessor.g.cs", writer.Files.Keys);
    }

    [Fact]
    public async Task Generate_AlwaysEmitsProcessorBaseAndProjectFile()
    {
        (_, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget"]));

        Assert.Contains("Processors/ProcessorBase.g.cs", writer.Files.Keys);
        Assert.Contains(writer.Files.Keys, k => k.EndsWith(".csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_MapsGeneratedClassesToTheirPaths()
    {
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["restoreVolume"]));

        Assert.Equal("Models/Widget.g.cs", result.GeneratedClasses["Widget"]);
        Assert.Equal("Processors/RestoreVolumeProcessor.g.cs", result.GeneratedClasses["RestoreVolumeProcessor"]);
        Assert.Equal("Processors/ProcessorBase.g.cs", result.GeneratedClasses["ProcessorBase"]);

        // Non-type artifacts (the project file) are not classes.
        Assert.DoesNotContain(result.GeneratedClasses.Values, v => v.EndsWith(".csproj", StringComparison.Ordinal));
        // Every mapped class points at a file that was actually written.
        foreach (string path in result.GeneratedClasses.Values)
        {
            Assert.Contains(path, writer.Files.Keys);
        }
    }

    [Fact]
    public async Task Generate_ProducesParseableCSharp()
    {
        (_, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget"], operations: ["get"]));

        string model = writer.Files["Models/Widget.g.cs"];
        Assert.Contains("namespace Contoso.Clients.Models", model);
        Assert.Contains("class Widget", model);

        // Every emitted .cs file must be valid C# (no syntax-error diagnostics).
        foreach ((string path, string source) in writer.Files.Where(f => f.Key.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public async Task Generate_ReportsWarningForEmptyModelName()
    {
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: [""]), new GeneratorOptions());

        Assert.Contains(result.Diagnostics, d => d.Code == "ace-generator/empty-model-name");
        Assert.DoesNotContain(writer.Files.Keys, k => k.StartsWith("Models/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_EmitsOnlyRelativeForwardSlashPaths()
    {
        (_, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget"], operations: ["get"]));

        Assert.NotEmpty(writer.Files);
        foreach (string path in writer.Files.Keys)
        {
            Assert.False(path.StartsWith('/'), $"'{path}' must be relative");
            Assert.False(path.Contains('\\'), $"'{path}' must use '/' separators");
            Assert.False(path.Contains(".."), $"'{path}' must not escape the base directory");
        }
    }
}
