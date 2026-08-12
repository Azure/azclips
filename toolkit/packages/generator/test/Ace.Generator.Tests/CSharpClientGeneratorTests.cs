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

    [Fact]
    public void Generate_EmitsAModelFilePerModel()
    {
        var generator = new CSharpClientGenerator();

        GenerationResult result = generator.Generate(
            Ir(models: ["Widget", "Gadget"]),
            new GeneratorOptions { RootNamespace = "Contoso.Clients" });

        Assert.True(result.Success);
        Assert.Contains("Models/Widget.g.cs", result.Files.Keys);
        Assert.Contains("Models/Gadget.g.cs", result.Files.Keys);
    }

    [Fact]
    public void Generate_EmitsAProcessorFilePerOperation()
    {
        var generator = new CSharpClientGenerator();

        // Operation names are camelCase in the IR; processors are PascalCased.
        GenerationResult result = generator.Generate(
            Ir(operations: ["restoreVolume"]),
            new GeneratorOptions { RootNamespace = "Contoso.Clients" });

        Assert.True(result.Success);
        Assert.Contains("Processors/RestoreVolumeProcessor.g.cs", result.Files.Keys);
    }

    [Fact]
    public void Generate_AlwaysEmitsProcessorBaseAndProjectFile()
    {
        var generator = new CSharpClientGenerator();

        GenerationResult result = generator.Generate(
            Ir(models: ["Widget"]),
            new GeneratorOptions { RootNamespace = "Contoso.Clients" });

        Assert.Contains("Processors/ProcessorBase.g.cs", result.Files.Keys);
        Assert.Contains(result.Files.Keys, k => k.EndsWith(".csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_ProducesParseableCSharp()
    {
        var generator = new CSharpClientGenerator();

        GenerationResult result = generator.Generate(
            Ir(models: ["Widget"], operations: ["get"]),
            new GeneratorOptions { RootNamespace = "Contoso.Clients" });

        string model = result.Files["Models/Widget.g.cs"];
        Assert.Contains("namespace Contoso.Clients.Models", model);
        Assert.Contains("class Widget", model);

        // Every emitted .cs file must be valid C# (no syntax-error diagnostics).
        foreach ((string path, string source) in result.Files.Where(f => f.Key.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public void Generate_ReportsWarningForEmptyModelName()
    {
        var generator = new CSharpClientGenerator();

        GenerationResult result = generator.Generate(Ir(models: [""]), new GeneratorOptions());

        Assert.Contains(result.Diagnostics, d => d.Code == "ace-generator/empty-model-name");
        Assert.DoesNotContain(result.Files.Keys, k => k.StartsWith("Models/", StringComparison.Ordinal));
    }
}
