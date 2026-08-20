using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Emits the real ElasticSan IR (TestData/elasticsan.ir.json) and checks the
/// <em>model</em> output. Faithful "garbage in, garbage out" emission — we only
/// assert the models are shaped from the IR and are syntactically valid C#;
/// processor/base/project artifacts are ignored here. Output lands in
/// <c>artifacts/ElasticSan_Models/</c> for eyeballing.
/// </summary>
public sealed class ElasticSanModelTests
{
    private static readonly string IrPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ir.json");

    private static IRDocument LoadIr() => IrJson.Deserialize(File.ReadAllText(IrPath));

    [Fact]
    public async Task Emits_A_Model_File_Per_Ir_Model()
    {
        IRDocument ir = LoadIr();
        var writer = new ArtifactWriter("ElasticSan_Models");

        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        Assert.True(result.Success);

        // One model file per IR model (ignore processor/base/project artifacts).
        int modelFiles = writer.Files.Keys.Count(k => k.StartsWith("Models/", StringComparison.Ordinal));
        Assert.Equal(ir.Models.Count, modelFiles);
    }

    [Fact]
    public async Task Emitted_Models_Are_Parseable_And_Carry_Properties()
    {
        IRDocument ir = LoadIr();
        var writer = new ArtifactWriter("ElasticSan_Models_Parse");

        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        // Every generated model must be valid C# (syntactically).
        foreach ((string path, string source) in writer.Files.Where(f => f.Key.StartsWith("Models/", StringComparison.Ordinal)))
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }

        // Spot-check a known model carries its properties + wire names.
        string volume = writer.Files["Models/VolumeProperties.g.cs"];
        Assert.Contains("namespace Azure.ElasticSan.Models", volume);
        Assert.Contains("public partial class VolumeProperties", volume);
        Assert.Contains("[JsonPropertyName(", volume);
    }
}
