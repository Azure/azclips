using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Pins the shape of the generated processor runtime base (<c>ProcessorBase</c> +
/// <c>ProcessorBase&lt;TResponse&gt;</c>). The base is not IR-driven, so a minimal empty IR is
/// enough. Output lands in <c>artifacts/ProcessorBase_*/</c> for eyeballing.
/// </summary>
public sealed class ProcessorBaseTests
{
    private static async Task<string> EmitBaseAsync(string scenario)
    {
        var writer = new ArtifactWriter(scenario);
        await new CSharpClientGenerator().GenerateAsync(
            new IRDocument { SchemaVersion = "0.6.0", Models = [], Operations = [] },
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);
        return writer.Files["Processors/ProcessorBase.g.cs"];
    }

    [Fact]
    public async Task Emits_Both_Marker_And_Generic_Base()
    {
        string src = await EmitBaseAsync(nameof(Emits_Both_Marker_And_Generic_Base));

        Assert.Contains("public abstract partial class ProcessorBase : IProcessor", src);
        Assert.Contains("public partial class ProcessorBase<TResponse> : ProcessorBase where TResponse : class", src);
    }

    [Fact]
    public async Task Base_Has_Lro_Machinery_Hooks_And_Seam()
    {
        string src = await EmitBaseAsync(nameof(Base_Has_Lro_Machinery_Hooks_And_Seam));

        foreach (string hook in new[] { "GetFinalUri", "GetOperationLocationUri", "GetAsyncOperationUri", "GetLocationUri" })
        {
            Assert.Contains($"Func<HttpResponseMessage, string?> {hook} => _ => null;", src);
        }

        Assert.Contains("async Task<HttpResponseMessage> SendAsyncLRO()", src);
        Assert.Contains("CreateExtendedProcessor<T>(Action<T>? configure = null)", src);
        // The subscriptionId reconciliation is left as a marked seam.
        Assert.Contains("SEAM(subscriptionId)", src);
    }

    [Fact]
    public async Task Base_Is_Parseable()
    {
        string src = await EmitBaseAsync(nameof(Base_Is_Parseable));

        var tree = CSharpSyntaxTree.ParseText(src);
        Assert.DoesNotContain(
            tree.GetDiagnostics(),
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }
}
