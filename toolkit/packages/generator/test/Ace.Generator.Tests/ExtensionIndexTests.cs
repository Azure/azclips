using System.Text.Json;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Emits the extension index from the real ElasticSan PowerShell surface
/// (<c>TestData/elasticsan.ps.json</c>, produced by the `ps.derive` transform over
/// <c>elasticsan.ir.json</c>) and checks the table it produces. Output lands in
/// <c>artifacts/ElasticSan_ExtensionIndex/</c> for eyeballing.
/// </summary>
public sealed class ExtensionIndexTests
{
    private static readonly string IrPath = Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ir.json");
    private static readonly string PsPath = Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ps.json");

    /// <summary>
    /// The PS surface is kept beside the IR rather than inside it: the committed IR is emitter
    /// output, and the surface is what a transform adds later in the pipeline.
    /// </summary>
    private static IRDocument LoadIr()
    {
        IRDocument ir = IrJson.Deserialize(File.ReadAllText(IrPath));
        ir.Ps = JsonSerializer.Deserialize<PS>(File.ReadAllText(PsPath), IrJson.Options);
        return ir;
    }

    private static async Task<ArtifactWriter> EmitAsync(string scenario, IRDocument ir)
    {
        var writer = new ArtifactWriter(scenario);
        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);
        return writer;
    }

    [Fact]
    public async Task Emits_One_Table_Entry_Per_Parameter_Set()
    {
        IRDocument ir = LoadIr();
        int sets = ir.Ps!.Commands.Where(c => c.Hidden != true).Sum(c => c.ParameterSets.Count);

        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex", ir);
        string index = writer.Files["ExtensionIndex.g.cs"];

        Assert.Equal(sets, index.Split("InvocationName:").Length - 1);
    }

    [Fact]
    public async Task Emitted_Index_Is_Parseable()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Parse", LoadIr());

        var tree = CSharpSyntaxTree.ParseText(writer.Files["ExtensionIndex.g.cs"]);
        Assert.DoesNotContain(
            tree.GetDiagnostics(),
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Suffixes_The_Invocation_Name_Only_When_A_Cmdlet_Has_Several_Sets()
    {
        IRDocument ir = LoadIr();
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Names", ir);
        string index = writer.Files["ExtensionIndex.g.cs"];

        foreach (PSCommandAttribute command in ir.Ps!.Commands.Where(c => c.Hidden != true))
        {
            string bare = $"{command.Verb}-{command.Noun}";
            if (command.ParameterSets.Count == 1)
            {
                Assert.Contains($"InvocationName: \"{bare}\"", index);
            }
            else
            {
                foreach (PSParameterSetAttribute set in command.ParameterSets)
                {
                    Assert.Contains($"InvocationName: \"{bare}_{set.Name}\"", index);
                }
            }
        }
    }

    [Fact]
    public async Task Binds_Each_Set_To_The_Processor_For_Its_Source_Operation()
    {
        IRDocument ir = LoadIr();
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Processors", ir);
        string index = writer.Files["ExtensionIndex.g.cs"];

        // Every processor the index names must be a file the processor stage actually emitted.
        foreach (string line in index.Split('\n').Where(l => l.Contains("ProcessorType: typeof(", StringComparison.Ordinal)))
        {
            string typeName = line.Split("typeof(Processors.")[1].Split(')')[0];
            Assert.True(
                writer.Files.ContainsKey($"Processors/{typeName}.g.cs"),
                $"index references {typeName}, which the processor stage did not emit");
        }
    }

    [Fact]
    public async Task Emits_No_Index_When_The_Document_Carries_No_PowerShell_Surface()
    {
        IRDocument ir = IrJson.Deserialize(File.ReadAllText(IrPath));
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Absent", ir);

        Assert.DoesNotContain("ExtensionIndex.g.cs", writer.Files.Keys);
    }

    [Fact]
    public async Task Omits_A_Hidden_Command_From_The_Table()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute hidden = ir.Ps!.Commands[0];
        hidden.Hidden = true;

        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Hidden", ir);
        string index = writer.Files["ExtensionIndex.g.cs"];

        Assert.DoesNotContain($"\"{hidden.Verb}-{hidden.Noun}\"", index);
        Assert.DoesNotContain($"\"{hidden.Verb}-{hidden.Noun}_", index);
    }

    [Fact]
    public async Task Reports_A_Set_Whose_Source_Operation_Is_Missing_Instead_Of_Emitting_A_Dangling_Reference()
    {
        IRDocument ir = LoadIr();
        ir.Ps!.Commands[0].ParameterSets[0].SourceOperationId = new IRId
        {
            Module = "nope",
            Namespace = ["Nope"],
            Name = "nope",
        };

        var writer = new ArtifactWriter("ElasticSan_ExtensionIndex_Unresolved");
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "ace-generator/unresolved-source-operation");
    }
}
