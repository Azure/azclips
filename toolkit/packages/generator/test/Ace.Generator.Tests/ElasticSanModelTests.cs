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

    private static TransformedIR LoadIr() => IrJson.Deserialize(File.ReadAllText(IrPath));

    [Fact]
    public async Task Emits_A_Model_File_Per_Ir_Model()
    {
        TransformedIR ir = LoadIr();
        var writer = new ArtifactWriter("ElasticSan_Models");

        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            config: null,
            writer.WriteAsync);

        Assert.True(result.Success);

        // One model file per IR model (ignore processor/base/project artifacts).
        Assert.Equal(ir.Models.Count, writer.Files.ModelSources().Count());
    }

    [Fact]
    public async Task Emitted_Models_Are_Parseable_And_Carry_Properties()
    {
        TransformedIR ir = LoadIr();
        var writer = new ArtifactWriter("ElasticSan_Models_Parse");

        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            config: null,
            writer.WriteAsync);

        // Every generated model must be valid C# (syntactically).
        foreach ((string _, string source) in writer.Files.ModelSources())
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }

        // Spot-check a known model carries its properties + wire names (looked up
        // by class name, not a hardcoded path).
        string volume = writer.Files.Source(result, "VolumeProperties");
        // Placement comes from the document's target, which outranks the option above.
        Assert.Contains("namespace Microsoft.ElasticSan.Models", volume);
        Assert.Contains("public partial class VolumeProperties", volume);
        Assert.Contains("[JsonPropertyName(", volume);
    }

    [Fact]
    public async Task Enum_Values_Read_From_The_Document_Are_Listed_As_Literals()
    {
        // A value read from JSON is a JsonElement, not the CLR string a hand-built document holds.
        var writer = new ArtifactWriter("ElasticSan_Models_Enum_Values");

        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            LoadIr(),
            config: null,
            writer.WriteAsync);

        string sku = writer.Files.Source(result, "Sku");
        Assert.Contains("/// Known values (extensible enum SkuName):", sku);
        Assert.Contains("/// <item><description>\"Premium_LRS\": Premium locally redundant storage</description></item>", sku);
    }
}
