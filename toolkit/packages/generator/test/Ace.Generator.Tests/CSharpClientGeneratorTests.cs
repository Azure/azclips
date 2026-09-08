using System.Runtime.CompilerServices;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class CSharpClientGeneratorTests
{
    private static IRId ServerId() => new() { Module = "Test", Name = "server" };

    private static IRDocument Ir(IEnumerable<string>? models = null, IEnumerable<string>? operations = null)
    {
        return new IRDocument
        {
            SchemaVersion = "0.8.0",
            Servers =
            [
                new IRServerDefinition
                {
                    Id = ServerId(),
                    Endpoint = new IRServerLiteral { Url = "https://example.com" },
                },
            ],
            Models = (models ?? [])
                .Select(name => new IRModel
                {
                    Name = name,
                    Source = "Test",
                    Id = new IRId { Module = "Test", Name = name },
                })
                .ToList(),
            Operations = (operations ?? [])
                .Select(name => new IROperation { Name = name, Server = ServerId() })
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
    public async Task Generate_EmitsATypePerModel()
    {
        (GenerationResult result, _) = await RunAsync(Ir(models: ["Widget", "Gadget"]));

        Assert.True(result.Success);
        // Contract: one generated type per model, named after the model.
        Assert.Contains("Widget", result.GeneratedClasses.Keys);
        Assert.Contains("Gadget", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Generate_EmitsAProcessorTypePerOperation()
    {
        // Operation names are camelCase in the IR; processor types are PascalCased + "Processor".
        (GenerationResult result, _) = await RunAsync(Ir(operations: ["restoreVolume"]));

        Assert.True(result.Success);
        Assert.Contains("RestoreVolumeProcessor", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Generate_DistinguishesInterfacesWithinTheSameNamespace()
    {
        IRDocument ir = Ir();
        ir.Operations = new string?[] { "Widgets", "Gadgets", null }
            .Select(group => new IROperation
            {
                Id = new IRId
                {
                    Module = "Test",
                    Namespace = ["Contoso", "Inventory"],
                    Interface = group,
                    Name = "get",
                },
                Name = "get",
                Server = ServerId(),
            })
            .ToList();

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            new[] { "GetGadgetsProcessor", "GetProcessor", "GetWidgetsProcessor", "ProcessorBase" },
            result.GeneratedClasses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(4, writer.Files.ProcessorSources().Count());
    }

    [Fact]
    public async Task Generate_UsesDisplayNameAndSourceInterfaceForProcessorName()
    {
        IRDocument ir = Ir();
        ir.Operations =
        [
            new IROperation
            {
                Id = new IRId { Module = "Test", Interface = "Widgets", Name = "read" },
                Name = "fetch",
                Server = ServerId(),
            },
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Contains("class FetchWidgetsProcessor", writer.Files.Source(result, "FetchWidgetsProcessor"));
        Assert.DoesNotContain("ReadWidgetsProcessor", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Generate_DoesNotInferInterfaceFromNamespace()
    {
        IRDocument ir = Ir();
        ir.Operations =
        [
            new IROperation
            {
                Id = new IRId
                {
                    Module = "Test",
                    Namespace = ["Contoso", "Inventory"],
                    Name = "get",
                },
                Name = "get",
                Server = ServerId(),
            },
        ];

        (GenerationResult result, _) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Contains("GetProcessor", result.GeneratedClasses.Keys);
        Assert.DoesNotContain("GetInventoryProcessor", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Generate_AlwaysEmitsProcessorBaseAndProjectFile()
    {
        (GenerationResult result, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget"]));

        Assert.Contains("ProcessorBase", result.GeneratedClasses.Keys);
        // A .csproj is a platform requirement, not a volatile naming decision.
        Assert.Contains(writer.Files.Keys, k => k.EndsWith(".csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_MapsEachClassToAWrittenFile()
    {
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["restoreVolume"]));

        // Every mapped class points at a file that was actually written...
        foreach (string path in result.GeneratedClasses.Values)
        {
            Assert.Contains(path, writer.Files.Keys);
        }
        // ...the project file is not a class...
        Assert.DoesNotContain(result.GeneratedClasses.Values, v => v.EndsWith(".csproj", StringComparison.Ordinal));
        // ...and coverage is exactly model + operation + the always-on base.
        Assert.Equal(
            new[] { "ProcessorBase", "RestoreVolumeProcessor", "Widget" },
            result.GeneratedClasses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Output_paths_follow_the_convention()
    {
        // THE single place that pins the *incidental* output path scheme. A rename
        // of the layout/suffix should touch only this test; everything else asserts
        // the stable contract (class coverage, parseability, content).
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["restoreVolume"]));

        Assert.Equal("Models/Widget.cs", result.GeneratedClasses["Widget"]);
        Assert.Equal("Processors/RestoreVolumeProcessor.cs", result.GeneratedClasses["RestoreVolumeProcessor"]);
        Assert.Equal("Processors/ProcessorBase.cs", result.GeneratedClasses["ProcessorBase"]);
        Assert.Contains("Contoso.Clients.csproj", writer.Files.Keys);
    }

    [Fact]
    public async Task Generate_ProducesParseableCSharp()
    {
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["get"]));

        // Content contract: the model type exists in its API namespace (looked up
        // by class name, not by a hardcoded path).
        string model = writer.Files.Source(result, "Widget");
        Assert.Contains("namespace Contoso.Clients.Models", model);
        Assert.Contains("class Widget", model);

        // Every generated C# source must be valid C# (no syntax-error diagnostics).
        foreach ((string _, string source) in writer.Files.CSharpSources())
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
        (GenerationResult result, _) = await RunAsync(Ir(models: [""]), new GeneratorOptions());

        Assert.Contains(result.Diagnostics, d => d.Code == "ace-generator/empty-model-name");
        // The empty-named model is skipped, not emitted — only the always-on base remains.
        Assert.Equal("ProcessorBase", Assert.Single(result.GeneratedClasses.Keys));
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
