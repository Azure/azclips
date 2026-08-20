using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Emits the real ElasticSan IR (TestData/elasticsan.ir.json) and checks the
/// <em>processor</em> output. Faithful "garbage in, garbage out" emission — we assert one
/// processor per operation, that every file is syntactically valid C#, and spot-check the
/// request shape (base generic, URI, body, LRO send override). Output lands in
/// <c>artifacts/ElasticSan_Processors/</c> for eyeballing.
/// </summary>
public sealed class ElasticSanProcessorTests
{
    private static readonly string IrPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ir.json");

    private static IRDocument LoadIr() => IrJson.Deserialize(File.ReadAllText(IrPath));

    private static async Task<ArtifactWriter> EmitAsync(string scenario)
    {
        var writer = new ArtifactWriter(scenario);
        await new CSharpClientGenerator().GenerateAsync(
            LoadIr(),
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);
        return writer;
    }

    [Fact]
    public async Task Emits_A_Processor_File_Per_Ir_Operation()
    {
        IRDocument ir = LoadIr();
        ArtifactWriter writer = await EmitAsync("ElasticSan_Processors");

        int processorFiles = writer.Files.Keys.Count(k => k.StartsWith("Processors/", StringComparison.Ordinal)
            && k.EndsWith("Processor.g.cs", StringComparison.Ordinal));
        Assert.Equal(ir.Operations.Count, processorFiles);
    }

    [Fact]
    public async Task Emitted_Processors_Are_Parseable()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_Processors_Parse");

        foreach ((string _, string source) in writer.Files.Where(f => f.Key.StartsWith("Processors/", StringComparison.Ordinal)))
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public async Task Create_Processor_Has_Body_Uri_And_Lro_Send()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_Processors_Create");

        string create = writer.Files["Processors/CreateElasticSansProcessor.g.cs"];

        Assert.Contains("public partial class CreateElasticSansProcessor : ProcessorBase<ElasticSan>", create);
        // Members mirror the API path parameters, subscriptionId inline (not special-cased).
        Assert.Contains("public string SubscriptionId { get; set; }", create);
        Assert.Contains("public string ResourceGroupName { get; set; }", create);
        Assert.Contains("public string ElasticSanName { get; set; }", create);
        // Single request-body member serialized as JSON content.
        Assert.Contains("public ElasticSan Body { get; set; }", create);
        Assert.Contains("Content = JsonContent.Create(Body, options: JsonOptions)", create);
        // PUT with the resolved host, interpolated path members, and api-version.
        Assert.Contains("new HttpRequestMessage(HttpMethod.Put, uri)", create);
        Assert.Contains("https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ElasticSan/elasticSans/{ElasticSanName}?api-version=", create);
        // Long-running -> send override.
        Assert.Contains("SendAsync() => SendAsyncLRO();", create);
    }

    [Fact]
    public async Task Get_Processor_Has_No_Body_And_No_Lro_Send()
    {
        ArtifactWriter writer = await EmitAsync("ElasticSan_Processors_Get");

        string get = writer.Files["Processors/GetElasticSansProcessor.g.cs"];

        Assert.Contains("public partial class GetElasticSansProcessor : ProcessorBase<ElasticSan>", get);
        Assert.Contains("new HttpRequestMessage(HttpMethod.Get, uri)", get);
        Assert.DoesNotContain("JsonContent.Create", get);
        Assert.DoesNotContain("SendAsyncLRO", get);
    }
}
