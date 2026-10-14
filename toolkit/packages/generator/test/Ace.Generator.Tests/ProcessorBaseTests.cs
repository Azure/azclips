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
            new TransformedIR
            {
                SchemaVersion = "0.7.0",
                Models = [],
                Operations = [],
                Target = Targets.For("Azure.ElasticSan"),
            },
            config: null,
            writer.WriteAsync);
        return writer.Files.Source(result, "ProcessorBase");
    }

    /// <summary>Emit the base for a document whose single server declares <paramref name="endpoint"/>.</summary>
    private static async Task<string> EmitBaseForEndpointAsync(string scenario, IRServer endpoint)
    {
        var server = new IRId { Module = "M", Name = "server" };
        var writer = new ArtifactWriter(scenario);
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            new TransformedIR
            {
                SchemaVersion = "0.12.0",
                Sources = [new IRSource { Id = "M", Server = server }],
                Servers = [new IRServerDefinition { Id = server, Endpoint = endpoint }],
                Models = [],
                Operations = [],
                Target = Targets.For("Azure.ElasticSan"),
            },
            config: null,
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

    [Fact]
    public async Task Serializer_Default_Omits_Nulls_So_An_Unset_Property_Is_Not_Sent_As_Null()
    {
        string src = await EmitBaseAsync(nameof(Serializer_Default_Omits_Nulls_So_An_Unset_Property_Is_Not_Sent_As_Null));

        // Correctness, not payload size: an explicit null is how a PATCH says "clear this field",
        // and an unset flat parameter must not mean that.
        Assert.Contains("DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull", src);
        Assert.Contains("JsonOptions = DefaultJsonOptions;", src);
        // Still what the request body is serialized with.
        Assert.Contains("JsonContent.Create(body, body.GetType(), options: JsonOptions)", src);
        Assert.Contains("using System.Text.Json.Serialization;", src);
    }

    [Fact]
    public async Task Endpoint_Defaults_To_The_Servers_Declared_Literal()
    {
        string src = await EmitBaseForEndpointAsync(
            nameof(Endpoint_Defaults_To_The_Servers_Declared_Literal),
            new IRServerLiteral { Url = "https://graph.microsoft.com" });

        Assert.Contains("virtual string Endpoint => \"https://graph.microsoft.com\";", src);
    }

    [Fact]
    public async Task Endpoint_Defaults_To_A_Parameterized_Servers_Default_Value()
    {
        // The ARM shape: `{endpoint}` with a default, which every management spec emits.
        string src = await EmitBaseForEndpointAsync(
            nameof(Endpoint_Defaults_To_A_Parameterized_Servers_Default_Value),
            new IRServerParameterized
            {
                Template = "{endpoint}",
                Parameters =
                [
                    new IRServerParameter
                    {
                        Name = "endpoint",
                        WireName = "endpoint",
                        DefaultValue = new IRLiteralValueString { Value = "https://management.azure.com" },
                    },
                ],
            });

        Assert.Contains("virtual string Endpoint => \"https://management.azure.com\";", src);
    }

    [Fact]
    public async Task Endpoint_Interpolates_A_Public_Input_When_The_Spec_Declares_No_Default()
    {
        // The data-plane shape (https://{vaultName}.vault.azure.net): only the caller knows the
        // value, so the generator must not invent one — it surfaces the parameter instead.
        string src = await EmitBaseForEndpointAsync(
            nameof(Endpoint_Interpolates_A_Public_Input_When_The_Spec_Declares_No_Default),
            new IRServerParameterized
            {
                Template = "https://{account}.example.com",
                Parameters = [new IRServerParameter { Name = "accountName", WireName = "account" }],
            });

        Assert.Contains("protected virtual string Endpoint => $\"https://{AccountName}.example.com\";", src);
        Assert.Contains("public string AccountName { get; set; } = string.Empty;", src);
        Assert.DoesNotContain("management.azure.com", src);
    }

    [Fact]
    public async Task Endpoint_Itself_Becomes_A_Public_Input_When_The_Ir_Declares_No_Server()
    {
        // Nothing to derive at all, so the endpoint is a plain input rather than a fabricated default.
        string src = await EmitBaseAsync(nameof(Endpoint_Itself_Becomes_A_Public_Input_When_The_Ir_Declares_No_Server));

        Assert.Contains("public string Endpoint { get; set; } = string.Empty;", src);
        Assert.DoesNotContain("virtual string Endpoint", src);
        Assert.DoesNotContain("management.azure.com", src);
    }
}
