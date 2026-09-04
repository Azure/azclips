using System.Text.Json;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Equal(sets, index.Split("InvocationName:").Length - 1);
    }

    [Fact]
    public async Task Emitted_Index_Is_Parseable()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Parse", LoadIr());

        var tree = CSharpSyntaxTree.ParseText(writer.Files["ExtensionIndex.Ps.cs"]);
        Assert.DoesNotContain(
            tree.GetDiagnostics(),
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Parsing cannot see an unresolved name, so every type the table names must be one that
    /// exists: a BCL type, or a type the generator itself emitted.
    /// </summary>
    [Fact]
    public async Task Every_Type_The_Table_Names_Exists()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Types", LoadIr());

        HashSet<string> emitted = [.. writer.Files.Keys
            .Where(k => k.EndsWith(".cs", StringComparison.Ordinal))
            .Select(k => Path.GetFileName(k)[..^".cs".Length])];

        var root = CSharpSyntaxTree.ParseText(writer.Files["ExtensionIndex.Ps.cs"]).GetRoot();
        var unresolved = new List<string>();

        foreach (TypeOfExpressionSyntax typeOf in root.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            foreach (string name in NamesIn(typeOf.Type))
            {
                if (!Bcl.Contains(name) && !emitted.Contains(name))
                {
                    unresolved.Add(name);
                }
            }
        }

        Assert.Empty(unresolved);
    }

    private static readonly HashSet<string> Bcl =
    [
        "string", "bool", "sbyte", "short", "int", "long", "byte", "ushort", "uint", "ulong",
        "float", "double", "decimal", "object", "IList", "IDictionary",
    ];

    /// <summary>The type names a `typeof(...)` depends on, ignoring namespace qualifiers.</summary>
    private static IEnumerable<string> NamesIn(TypeSyntax type)
    {
        switch (type)
        {
            case QualifiedNameSyntax qualified:
                foreach (string name in NamesIn(qualified.Right)) yield return name;
                break;
            case GenericNameSyntax generic:
                yield return generic.Identifier.Text;
                foreach (TypeSyntax argument in generic.TypeArgumentList.Arguments)
                {
                    foreach (string name in NamesIn(argument)) yield return name;
                }
                break;
            case ArrayTypeSyntax array:
                foreach (string name in NamesIn(array.ElementType)) yield return name;
                break;
            case PredefinedTypeSyntax predefined:
                yield return predefined.Keyword.Text;
                break;
            case IdentifierNameSyntax identifier:
                yield return identifier.Identifier.Text;
                break;
        }
    }

    [Fact]
    public async Task Renders_An_Enum_Parameter_As_A_String_With_Its_Members_Allowed()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Enums", LoadIr());
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Contains("AllowedValues: [\"Enabled\", \"Disabled\"]", index);
        // The enum's own type name must not survive: the model stage emits no enum types.
        Assert.DoesNotContain("typeof(PublicNetworkAccess)", index);
    }

    [Fact]
    public async Task Suffixes_The_Invocation_Name_Only_When_A_Cmdlet_Has_Several_Sets()
    {
        IRDocument ir = LoadIr();
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Names", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

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
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        // Every processor the index names must be a file the processor stage actually emitted.
        foreach (string line in index.Split('\n').Where(l => l.Contains("ProcessorType: typeof(", StringComparison.Ordinal)))
        {
            string typeName = line.Split("typeof(Processors.")[1].Split(')')[0];
            Assert.True(
                writer.Files.ContainsKey($"Processors/{typeName}.cs"),
                $"index references {typeName}, which the processor stage did not emit");
        }
    }

    [Fact]
    public async Task Emits_No_Index_When_The_Document_Carries_No_PowerShell_Surface()
    {
        IRDocument ir = IrJson.Deserialize(File.ReadAllText(IrPath));
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Absent", ir);

        Assert.DoesNotContain("ExtensionIndex.cs", writer.Files.Keys);
        Assert.DoesNotContain("ExtensionIndex.Ps.cs", writer.Files.Keys);
    }

    /// <summary>
    /// The surfaces are generated independently, so the core must not name any one of them: it
    /// declares a hook per surface and each partial fills its own table.
    /// </summary>
    [Fact]
    public async Task Splits_The_Core_From_The_Surface_So_Each_Can_Be_Generated_Alone()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Partial", LoadIr());

        string core = writer.Files["ExtensionIndex.cs"];
        string ps = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Contains("public partial class ExtensionIndex : IExtensionIndex", core);
        Assert.Contains("static partial void AddPowerShellCommands", core);
        Assert.Contains("static partial void AddCliCommands", core);
        // No command data in the core, and no interface/lookup surface in the partial.
        Assert.DoesNotContain("InvocationName:", core);
        Assert.DoesNotContain("IExtensionIndex", ps);
        Assert.DoesNotContain("GetCommandTable", ps);
    }

    [Fact]
    public async Task Omits_A_Hidden_Command_From_The_Table()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute hidden = ir.Ps!.Commands[0];
        hidden.Hidden = true;

        ArtifactWriter writer = await EmitAsync("ElasticSan_ExtensionIndex_Hidden", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

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
