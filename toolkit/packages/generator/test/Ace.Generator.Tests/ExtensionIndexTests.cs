using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Emits the extension index from canonical ElasticSan commands. The committed fixture is emitter
/// output, so each test adds a controlled PowerShell dialect in memory just as a transform would.
/// </summary>
public sealed class ExtensionIndexTests
{
    private static readonly string IrPath = Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ir.json");

    private static TransformedIR LoadIr(bool annotatePowerShell = true)
    {
        TransformedIR ir = IrJson.Deserialize(File.ReadAllText(IrPath));
        if (annotatePowerShell)
        {
            foreach (IRCommand command in ir.Commands)
            {
                command.Dialect = new IRCommandDialect
                {
                    Ps = new PsCommandDialect
                    {
                        Verb = command.Id.Action,
                        Noun = command.Id.Subject,
                    },
                };
            }
        }

        return ir;
    }

    private static async Task<(ArtifactWriter Writer, GenerationResult Result)> EmitAsync(
        string scenario,
        TransformedIR ir)
    {
        var writer = new ArtifactWriter(scenario);
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);
        return (writer, result);
    }

    [Fact]
    public async Task Emits_One_Table_Entry_Per_Visible_Canonical_Command()
    {
        TransformedIR ir = LoadIr();
        int visible = ir.Commands.Count(command => command.Dialect?.Ps is { Hidden: not true });

        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Equal(visible, index.Split("InvocationName:").Length - 1);
    }

    [Fact]
    public async Task Suffixes_Only_Sibling_Invocation_Bases_With_The_Canonical_Variant()
    {
        TransformedIR ir = LoadIr();
        Dictionary<string, int> counts = ir.Commands
            .GroupBy(command => $"{command.Dialect!.Ps!.Verb}-{command.Dialect.Ps.Noun}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex_Names", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        foreach (IRCommand command in ir.Commands)
        {
            string invocationBase = $"{command.Dialect!.Ps!.Verb}-{command.Dialect.Ps.Noun}";
            string expected = counts[invocationBase] > 1
                ? $"{invocationBase}_{command.Id.Variant}"
                : invocationBase;
            Assert.Contains($"InvocationName: \"{expected}\"", index);
        }

        Assert.Contains("InvocationName: \"List-ElasticSans_ByResourceGroup\"", index);
        Assert.Contains("InvocationName: \"List-ElasticSans_BySubscription\"", index);
    }

    [Fact]
    public async Task Every_Visible_Command_Uses_Its_Canonical_Processor()
    {
        TransformedIR ir = LoadIr();
        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex_Processors", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        foreach (IRCommand command in ir.Commands.Where(command => command.Dialect?.Ps is { Hidden: not true }))
        {
            string processor = command.Name + "Processor";
            Assert.Contains($"ProcessorType: typeof(Processors.{processor})", index);
            Assert.True(
                writer.Files.ContainsKey($"Processors/{processor}.cs"),
                $"index references {processor}, which the processor stage did not emit");
        }
    }

    [Fact]
    public async Task Emits_No_Index_When_Commands_Have_No_PowerShell_Dialect()
    {
        (ArtifactWriter writer, _) = await EmitAsync(
            "ElasticSan_ExtensionIndex_Absent",
            LoadIr(annotatePowerShell: false));

        Assert.DoesNotContain("ExtensionIndex.cs", writer.Files.Keys);
        Assert.DoesNotContain("ExtensionIndex.Ps.cs", writer.Files.Keys);
    }

    [Fact]
    public async Task Omits_A_Dialect_Hidden_Command()
    {
        TransformedIR ir = LoadIr();
        IRCommand hidden = ir.Commands[0];
        hidden.Dialect!.Ps!.Hidden = true;

        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex_Hidden", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.DoesNotContain($"Name: \"{hidden.Name}Processor\"", index);
    }

    [Fact]
    public async Task Uses_Parameter_Dialect_And_Variant_Membership_With_Canonical_Fallbacks()
    {
        TransformedIR ir = LoadIr();
        IRCommand command = ir.Commands.First(candidate => candidate.Parameters.Count >= 3 && candidate.Id.Variant is not null);
        IRCommandParameter variantParameter = command.Parameters[0];
        IRCommandParameter unnamedParameter = command.Parameters[1];
        IRCommandParameter canonicalParameter = command.Parameters[2];

        variantParameter.Required = true;
        variantParameter.Docs = "Canonical docs ignored";
        variantParameter.Dialect = new IRCommandParameterDialect
        {
            Ps = new PsParameterDialect
            {
                Name = "DisplayName",
                Aliases = ["AliasOne", "AliasTwo"],
                Hidden = true,
                Description = "PowerShell help",
                ParameterSets =
                [
                    new PsParameterSetMembership { Set = null, Mandatory = true },
                    new PsParameterSetMembership { Set = command.Id.Variant, Mandatory = false },
                ],
            },
        };

        unnamedParameter.Required = false;
        unnamedParameter.Docs = "Canonical docs help";
        unnamedParameter.Dialect = new IRCommandParameterDialect
        {
            Ps = new PsParameterDialect
            {
                ParameterSets = [new PsParameterSetMembership { Set = null, Mandatory = true }],
            },
        };

        canonicalParameter.Required = true;
        canonicalParameter.Docs = null;
        canonicalParameter.Summary = "Canonical summary help";
        canonicalParameter.Dialect = null;

        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex_Parameters", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Contains(
            $"[{Quote(variantParameter.Name)}] = new({Quote(variantParameter.Name)}, \"DisplayName\", "
                + $"typeof({RenderedType(index, variantParameter.Name)}), \"PowerShell help\", IsRequired: false, "
                + "Aliases: [\"AliasOne\", \"AliasTwo\"]",
            index);
        Assert.Contains(
            $"[{Quote(unnamedParameter.Name)}] = new({Quote(unnamedParameter.Name)}, {Quote(unnamedParameter.Name)}, "
                + $"typeof({RenderedType(index, unnamedParameter.Name)}), \"Canonical docs help\", IsRequired: true",
            index);
        Assert.Contains(
            $"[{Quote(canonicalParameter.Name)}] = new({Quote(canonicalParameter.Name)}, {Quote(canonicalParameter.Name)}, "
                + $"typeof({RenderedType(index, canonicalParameter.Name)}), \"Canonical summary help\", IsRequired: true",
            index);
        Assert.Contains($"[\"AliasOne\"] = {Quote(variantParameter.Name)}", index);
        Assert.Contains($"[\"AliasTwo\"] = {Quote(variantParameter.Name)}", index);
    }

    [Fact]
    public async Task Renders_Enum_Parameters_As_Strings_With_Document_Enum_Members()
    {
        TransformedIR ir = LoadIr();
        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex_Enums", ir);
        string index = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Contains("AllowedValues: [\"Enabled\", \"Disabled\"]", index);
        Assert.DoesNotContain("typeof(PublicNetworkAccess)", index);
    }

    [Fact]
    public async Task Emitted_Index_Files_Are_Parseable()
    {
        (ArtifactWriter writer, _) = await EmitAsync("ElasticSan_ExtensionIndex_Parse", LoadIr());

        foreach (string path in new[] { "ExtensionIndex.cs", "ExtensionIndex.Ps.cs" })
        {
            var tree = CSharpSyntaxTree.ParseText(writer.Files[path]);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public async Task Splits_Core_From_Surface_And_Claims_The_Class_Once()
    {
        (ArtifactWriter writer, GenerationResult result) = await EmitAsync(
            "ElasticSan_ExtensionIndex_Partial",
            LoadIr());
        string core = writer.Files["ExtensionIndex.cs"];
        string ps = writer.Files["ExtensionIndex.Ps.cs"];

        Assert.Contains("public partial class ExtensionIndex : IExtensionIndex", core);
        Assert.Contains("static partial void AddPowerShellCommands", core);
        Assert.Contains("static partial void AddCliCommands", core);
        Assert.DoesNotContain("InvocationName:", core);
        Assert.DoesNotContain("IExtensionIndex", ps);
        Assert.DoesNotContain("GetCommandTable", ps);
        Assert.Equal("ExtensionIndex.cs", result.GeneratedClasses["ExtensionIndex"]);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ace-generator/duplicate-class");
    }

    [Fact]
    public async Task Diagnoses_A_Remaining_Runtime_Key_Collision()
    {
        TransformedIR ir = LoadIr();
        IRCommand first = ir.Commands[0];
        IRCommand second = ir.Commands[1];
        first.Dialect!.Ps = new PsCommandDialect { Verb = "Get", Noun = "Collision" };
        second.Dialect!.Ps = new PsCommandDialect { Verb = "Get", Noun = "Collision" };
        first.Id.Variant = "Same";
        second.Id.Variant = "Same";

        (ArtifactWriter writer, GenerationResult result) = await EmitAsync(
            "ElasticSan_ExtensionIndex_Collision",
            ir);

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Code == "ace-generator/duplicate-command-invocation"
                && diagnostic.Message.Contains("Get-Collision_Same", StringComparison.Ordinal));
        Assert.DoesNotContain("ExtensionIndex.cs", writer.Files.Keys);
        Assert.DoesNotContain("ExtensionIndex.Ps.cs", writer.Files.Keys);
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string RenderedType(string index, string parameterName)
    {
        string marker = $"[{Quote(parameterName)}] = new(";
        string entry = index[index.IndexOf(marker, StringComparison.Ordinal)..];
        return entry.Split("typeof(", 2, StringSplitOptions.None)[1].Split(')', 2)[0];
    }
}
