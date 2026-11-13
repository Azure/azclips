using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Placement comes from the document.
///
/// A namespace and a project name are *decisions*, resolved by the <c>seed-ir</c> stage
/// from the document facts. The generator spells them; it does not invent them. The
/// folders inside the project are the generator's own convention. These pin that the
/// document's target decides, and that a document without one is rejected rather than
/// placed somewhere by default.
/// </summary>
public sealed class PlacementTests
{
    private static IRId Id(string name) => new() { Module = "M", Namespace = ["N"], Name = name };

    private static TransformedIR Document(IRTarget? target)
    {
        var document = new TransformedIR
        {
            SchemaVersion = "0.13.0",
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
            Commands = [],
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
            ir, config: null, files.WriteAsync);
        return (result, files);
    }

    [Fact]
    public async Task Namespace_and_project_come_from_the_documents_target_and_the_folders_are_fixed()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(
            Document(new IRTarget
            {
                RootNamespace = ["Contoso", "Widgets"],
                ProjectName = "Contoso.Widgets.Client",
            }));

        Assert.True(result.Success);

        // Files land in the generator's own folders.
        Assert.Contains(files.Files.Keys, path => path == "Models/Widget.cs");
        Assert.Contains(files.Files.Keys, path => path.StartsWith("Processors/", StringComparison.Ordinal));

        // Namespaces follow the folders, as C# projects conventionally do.
        Assert.Contains("namespace Contoso.Widgets.Models;", files.Files["Models/Widget.cs"]);

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
}
