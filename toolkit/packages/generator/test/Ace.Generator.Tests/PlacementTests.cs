using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Placement comes from the document.
///
/// A namespace, a project name, and an output folder are *decisions*, resolved by the
/// <c>seed-ir</c> stage from the document facts. The generator spells them; it does not
/// invent them. These pin that the document's target decides, and that a document
/// without one is rejected rather than placed somewhere by default.
/// </summary>
public sealed class PlacementTests
{
    private static IRId Id(string name) => new() { Module = "M", Namespace = ["N"], Name = name };

    private static TransformedIR Document(IRTarget? target)
    {
        var document = new TransformedIR
        {
            SchemaVersion = "0.12.0",
            Models =
            [
                new IRModel
                {
                    Id = Id("Widget"),
                    Name = "Widget",
                    Source = "M",
                    Properties = [],
                },
            ],
            Operations = [],
        };
        if (target is not null)
        {
            document.Target = target;
        }

        return document;
    }

    private static async Task<(GenerationResult Result, GeneratedFileCollector Files)> RunAsync(TransformedIR ir)
    {
        var files = new GeneratedFileCollector();
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir, new GeneratorOptions(), files.WriteAsync);
        return (result, files);
    }

    [Fact]
    public async Task Namespace_project_and_directories_come_from_the_documents_target()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(
            Document(new IRTarget
            {
                RootNamespace = ["Contoso", "Widgets"],
                ProjectName = "Contoso.Widgets.Client",
                Directories = new IRTargetDirectories { Models = "Types", Processors = "Commands" },
            }));

        Assert.True(result.Success);

        // Files land in the configured folders.
        Assert.Contains(files.Files.Keys, path => path == "Types/Widget.cs");
        Assert.Contains(files.Files.Keys, path => path.StartsWith("Commands/", StringComparison.Ordinal));

        // Namespaces follow the folders, as C# projects conventionally do.
        Assert.Contains("namespace Contoso.Widgets.Types;", files.Files["Types/Widget.cs"]);

        // The project name may differ from the namespace, and does here.
        string csproj = files.Files.First(entry => entry.Key.EndsWith(".csproj", StringComparison.Ordinal)).Value;
        Assert.Contains("<AssemblyName>Contoso.Widgets.Client</AssemblyName>", csproj);
        Assert.Contains("<RootNamespace>Contoso.Widgets</RootNamespace>", csproj);
    }

    [Fact]
    public async Task A_document_without_a_target_is_rejected()
    {
        // A document from the pipeline always has one: seed-ir writes it. There is no
        // fallback namespace, so nothing is generated.
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document(target: null));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "ace-generator/missing-target");
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task A_nested_output_folder_still_yields_a_legal_namespace_segment()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(
            Document(new IRTarget
            {
                RootNamespace = ["Contoso"],
                ProjectName = "Contoso",
                Directories = new IRTargetDirectories { Models = "src/Models", Processors = "src/Processors" },
            }));

        Assert.True(result.Success);
        Assert.Contains(files.Files.Keys, path => path == "src/Models/Widget.cs");
        // The path nests; the namespace takes only the trailing segment.
        Assert.Contains("namespace Contoso.Models;", files.Files["src/Models/Widget.cs"]);
    }
}
