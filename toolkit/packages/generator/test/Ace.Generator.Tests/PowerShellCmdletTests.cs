using System.Text.Json;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class PowerShellCmdletTests
{
    private static readonly string IrPath = Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ir.json");
    private static readonly string PsPath = Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ps.json");

    private static IRDocument LoadIr()
    {
        IRDocument ir = IrJson.Deserialize(File.ReadAllText(IrPath));
        ir.Ps = JsonSerializer.Deserialize<PS>(File.ReadAllText(PsPath), IrJson.Options);
        return ir;
    }

    [Fact]
    public async Task Emits_A_Discoverable_Cmdlet_Class_Per_Parameter_Set()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute command = ir.Ps!.Commands.First(c => c.Hidden != true && c.ParameterSets.Count > 1);
        PSParameterSetAttribute set = command.ParameterSets[0];
        var writer = new GeneratedFileCollector();

        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        string invocationName = $"{command.Verb}-{command.Noun}_{set.Name}";
        string className = $"{command.Verb}{command.Noun}_{set.Name}";
        string path = $"Cmdlets/{className}.cs";
        string source = writer.Files[path];

        Assert.Contains($"public sealed class {className} : GeneratedCmdletBase", source);
        Assert.Contains("public abstract class GeneratedCmdletBase : PSCmdlet", writer.Files["Cmdlets/GeneratedCmdletBase.cs"]);
        Assert.Contains($"[Cmdlet(\"{command.Verb}\", \"{command.Noun}_{set.Name}\"", source);
        Assert.Contains("[OutputType(", source);
        Assert.Contains($"ParameterSetName = \"{set.Name}\"", source);

        var tree = CSharpSyntaxTree.ParseText(source);
        Assert.DoesNotContain(
            tree.GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Generated_Cmdlets_Dispatch_Through_The_Extension_Index_Invocation_Name()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute command = ir.Ps!.Commands.First(c => c.Hidden != true && c.ParameterSets.Count > 1);
        PSParameterSetAttribute set = command.ParameterSets[0];
        var writer = new GeneratedFileCollector();

        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        string className = $"{command.Verb}{command.Noun}_{set.Name}";
        string source = writer.Files[$"Cmdlets/{className}.cs"];
        string cmdletBase = writer.Files["Cmdlets/GeneratedCmdletBase.cs"];

        Assert.Contains("GeneratedCmdletBase", source);
        Assert.Contains($"InvocationName => \"{command.Verb}-{command.Noun}_{set.Name}\"", source);
        Assert.Contains("ExtensionName => \"ElasticSan\"", source);
        Assert.Contains("engine.OpenExecutionSession(invocation, streams, cancellationTokenSource.Token)", cmdletBase);
        Assert.Contains("MyInvocation.BoundParameters", cmdletBase);
    }

    [Fact]
    public async Task Emits_Exporter_Metadata_And_Enum_Validation()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute command = ir.Ps!.Commands.First(c => c.Hidden != true && c.HttpPath is not null);
        PSParameterSetAttribute set = command.ParameterSets[0];
        var writer = new GeneratedFileCollector();

        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        string suffix = command.ParameterSets.Count > 1 ? $"_{set.Name}" : string.Empty;
        string source = writer.Files[$"Cmdlets/{command.Verb}{command.Noun}{suffix}.cs"];

        Assert.Contains("[Generated]", source);
        Assert.Contains("[Description(", source);
        Assert.Contains($"Path = \"{command.HttpPath!.Path}\"", source);
        Assert.Contains($"ApiVersion = \"{command.HttpPath.ApiVersion}\"", source);
        Assert.Contains(
            writer.Files.Where(file => file.Key.StartsWith("Cmdlets/", StringComparison.Ordinal)).Select(file => file.Value),
            cmdlet => cmdlet.Contains("[ValidateSet("));
    }

    [Fact]
    public async Task Emits_Http_Metadata_From_The_Parameter_Set_Operation()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute command = ir.Ps!.Commands.Single(c => c.Verb == "Get" && c.Noun == "AzElasticSan");
        PSParameterSetAttribute set = command.ParameterSets.Single(s => s.Name == "Get");
        IROperation operation = ir.Operations.Single(candidate => candidate.Id.Name == set.SourceOperationId.Name
            && candidate.Id.Namespace.SequenceEqual(set.SourceOperationId.Namespace));
        var writer = new GeneratedFileCollector();

        Assert.Null(command.HttpPath);

        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        string source = writer.Files["Cmdlets/GetAzElasticSan_Get.cs"];
        Assert.Contains($"Path = \"{operation.PathTemplate}\"", source);
        Assert.Contains($"ApiVersion = \"{operation.ApiVersion.Value}\"", source);
    }

    [Fact]
    public async Task Emits_Parameter_Customization_Metadata()
    {
        IRDocument ir = LoadIr();
        PSCommandAttribute command = ir.Ps!.Commands.First(c => c.Hidden != true);
        PSParameterSetAttribute set = command.ParameterSets[0];
        PSParameterAttribute parameter = command.Parameters.First(p =>
            p.ParameterSets is { } sets ? sets.ContainsKey(set.Name) : p.ParameterSetName is null || p.ParameterSetName == set.Name);
        parameter.Hidden = true;
        parameter.Completer = new PSCompleterAttribute
        {
            Script = "$wordToComplete",
            Name = "resource",
            Description = "Completes resource names",
        };
        parameter.DefaultValue = new PSDefaultValueAttribute
        {
            Script = "$DefaultSubscription",
            Name = "subscription",
            Description = "The active subscription",
            SetCondition = "$null -eq $SubscriptionId",
        };
        var writer = new GeneratedFileCollector();

        await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);

        string suffix = command.ParameterSets.Count > 1 ? $"_{set.Name}" : string.Empty;
        string source = writer.Files[$"Cmdlets/{command.Verb}{command.Noun}{suffix}.cs"];

        Assert.Contains("[DoNotExport]", source);
        Assert.Contains("[CompleterInfo(", source);
        Assert.Contains("Script = \"$wordToComplete\"", source);
        Assert.Contains("[DefaultInfo(", source);
        Assert.Contains("SetCondition = \"$null -eq $SubscriptionId\"", source);
    }
}
