using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Covers <c>IRModel.AdditionalProperties</c> (TypeSpec <c>Record&lt;T&gt;</c>): a pure record renders
/// as a dictionary at its use sites, and a record with properties carries its open map as
/// extension data.
/// </summary>
public sealed class RecordModelTests
{
    private const string Module = "M";

    private static IRId Id(string name) => new() { Module = Module, Namespace = ["S"], Name = name };

    private static IRTypeRefPrimitive String => new() { Name = IRTypeRefPrimitiveName.String };

    /// <summary>Blob { metadata -&gt; Metadata (Record&lt;string&gt; + encrypted), orMetadata -&gt; OrMetadata (Record&lt;string&gt;) }.</summary>
    private static TransformedIR Document() => new()
    {
        SchemaVersion = "0.12.0",
        Sources = [new IRSource { Id = Module, Server = Id("server") }],
        Servers = [new IRServerDefinition { Id = Id("server"), Endpoint = new IRServerLiteral { Url = "https://example.com" } }],
        Models =
        [
            new IRModel
            {
                Id = Id("Blob"),
                Name = "Blob",
                Source = Module,
                Properties =
                [
                    new IRPropertyObject { Id = Id("Blob.metadata"), Name = "Metadata", WireName = "metadata", Optional = true, Type = new IRTypeRefModel { Id = Id("Metadata") } },
                    new IRPropertyObject { Id = Id("Blob.orMetadata"), Name = "OrMetadata", WireName = "orMetadata", Optional = true, Type = new IRTypeRefModel { Id = Id("OrMetadata") } },
                ],
            },
            new IRModel
            {
                Id = Id("Metadata"),
                Name = "Metadata",
                Source = Module,
                Properties = [new IRPropertyPrimitive { Id = Id("Metadata.encrypted"), Name = "Encrypted", WireName = "encrypted", Optional = true, Type = String }],
                AdditionalProperties = String,
            },
            new IRModel { Id = Id("OrMetadata"), Name = "OrMetadata", Source = Module, AdditionalProperties = String },
        ],
        Operations = [],
        Target = Targets.For("Contoso.Clients"),
    };

    [Fact]
    public async Task A_Pure_Record_Is_A_Dictionary_And_A_Record_With_Properties_Carries_Extension_Data()
    {
        var files = new GeneratedFileCollector();
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(Document(), config: null, files.WriteAsync);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.False(result.GeneratedClasses.ContainsKey("OrMetadata"));

        string blob = files.Files.Source(result, "Blob");
        Assert.Contains("public IDictionary<string, string>? OrMetadata", blob);
        Assert.Contains("public Metadata? Metadata", blob);

        string metadata = files.Files.Source(result, "Metadata");
        Assert.Contains("public string? Encrypted", metadata);
        Assert.Contains("[JsonExtensionData]", metadata);
        Assert.Contains("public IDictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();", metadata);
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(metadata).GetDiagnostics(),
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }
}
