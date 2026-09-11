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
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            new IRDocument { SchemaVersion = "0.6.0", Models = [], Operations = [] },
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);
        return writer.Files.Source(result, "ProcessorBase");
    }

    [Fact]
    public async Task Emits_Both_Marker_And_Generic_Base()
    {
        string src = await EmitBaseAsync(nameof(Emits_Both_Marker_And_Generic_Base));

        Assert.Contains("public abstract partial class ProcessorBase : IProcessor", src);
        Assert.Contains("public partial class ProcessorBase<TRequest, TResponse> : ProcessorBase where TRequest : class where TResponse : class", src);
        // Runtime deps injected in Init (incl. the auth handler) and copied to children.
        Assert.Contains("IAuthHandler AuthHandler { get; private set; }", src);
        Assert.Contains("AuthHandler = context.AuthHandler;", src);
    }

    [Fact]
    public async Task Base_Has_Request_Seams_Lro_Hooks_And_Runtime_Delegation()
    {
        string src = await EmitBaseAsync(nameof(Base_Has_Request_Seams_Lro_Hooks_And_Runtime_Delegation));

        foreach (string hook in new[] { "GetFinalUri", "GetOperationLocationUri", "GetAsyncOperationUri", "GetLocationUri" })
        {
            Assert.Contains($"Func<HttpResponseMessage, string?> {hook} => _ => null;", src);
        }

        // Fine-grained request seams the processors override.
        Assert.Contains("HttpMethod Method =>", src);
        Assert.Contains("string CreateRequestUri()", src);
        Assert.Contains("CreateRequestHeaders(HttpRequestMessage request)", src);
        // Typed body seam on the generic base (covariant override).
        Assert.Contains("TRequest? CreateRequestBody() => default;", src);
        // Transport delegates to the runtime library; the LRO loop is not inlined in the base.
        Assert.Contains("HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline)", src);
        Assert.Contains("HttpRuntime.SendAsyncLRO(CreateRequestMessage, Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri)", src);
        Assert.Contains("CreateExtendedProcessor<T>(Action<T>? configure = null)", src);
        // ProcessAsync pipeline hooks.
        Assert.Contains("BeforeProcessAsync()", src);
        Assert.Contains("BeforeWriteOutputAsyncAndReturn(TResponse? body)", src);
        // The subscriptionId base-resolution seam is gone (subscriptionId is now a per-processor member).
        Assert.DoesNotContain("ResolveSubscriptionId", src);
        Assert.DoesNotContain("SEAM(subscriptionId)", src);
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
