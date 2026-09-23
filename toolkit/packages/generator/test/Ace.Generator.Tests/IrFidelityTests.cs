using System.Text.Json;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class IrFidelityTests
{
    private static T RoundTrip<T>(string json)
    {
        T value = JsonSerializer.Deserialize<T>(json, IrJson.Options)!;
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, IrJson.Options), IrJson.Options)!;
    }

    [Fact]
    public void Source_Union_Definitions_References_And_Null_Default_Variants_RoundTrip()
    {
        TransformedIR document = RoundTrip<TransformedIR>(
            """
            {
              "schemaVersion": "0.11.0",
              "discriminatedUnions": [{
                "id": { "module": "M", "namespace": ["Service"], "name": "Choice" },
                "name": "Choice", "source": "M",
                "discriminatorPropertyName": "@kind",
                "envelope": "object", "envelopePropertyName": "payload",
                "variants": [
                  { "value": "", "nullable": false,
                    "type": { "id": { "module": "M", "namespace": ["Service"], "name": "Choice" },
                              "kind": "discriminatedUnion" } },
                  { "value": "nil", "nullable": true, "literalValue": { "kind": "null" } },
                  { "nullable": false, "type": { "kind": "primitive", "name": "string" },
                    "literalValue": { "value": "fallback", "kind": "string" } }
                ]
              }],
              "models": [{
                "name": "Holder", "source": "M",
                "id": { "module": "M", "namespace": ["Service"], "name": "Holder" },
                "properties": [{
                  "id": { "module": "M", "namespace": ["Service"], "name": "Holder.choice" },
                  "name": "Choice", "wireName": "choice", "optional": false, "nullable": false,
                  "type": { "id": { "module": "M", "namespace": ["Service"], "name": "Choice" },
                            "kind": "discriminatedUnion" },
                  "typeKind": "union"
                }]
              }]
            }
            """);

        IRDiscriminatedUnion union = Assert.Single(document.DiscriminatedUnions!);
        Assert.Equal(IRDiscriminatedUnionEnvelope.Object, union.Envelope);
        Assert.Equal("@kind", union.DiscriminatorPropertyName);
        Assert.Equal("payload", union.EnvelopePropertyName);
        Assert.Equal(3, union.Variants.Count);
        Assert.Equal("", union.Variants[0].Value);
        Assert.Equal(union.Id.Name, Assert.IsType<IRTypeRefDiscriminatedUnion>(union.Variants[0].Type).Id.Name);
        Assert.Null(union.Variants[1].Type);
        Assert.True(union.Variants[1].Nullable);
        Assert.IsType<IRLiteralValueNull>(union.Variants[1].LiteralValue);
        Assert.Null(union.Variants[2].Value);
        Assert.Equal("fallback", Assert.IsType<IRLiteralValueString>(union.Variants[2].LiteralValue).Value);
        var property = Assert.IsType<IRPropertyUnion>(Assert.Single(Assert.Single(document.Models).Properties));
        Assert.Equal(union.Id.Name, Assert.IsType<IRTypeRefDiscriminatedUnion>(property.Type).Id.Name);
    }

    [Fact]
    public void Complete_Paging_And_Lro_Metadata_RoundTrip_Together()
    {
        const string input = """
            {
              "sourcePath": [{
                "id": { "module": "M", "namespace": ["Service"], "interface": "Lists", "name": "list.cursor" },
                "name": "cursor"
              }],
              "type": { "kind": "primitive", "name": "string" }, "optional": true, "nullable": true,
              "bindings": [{
                "location": "query", "path": ["in.cursor"],
                "parameter": { "module": "M", "namespace": ["Service"], "interface": "Lists", "name": "list.cursor" }
              }]
            }
            """;
        const string output = """
            {
              "sourcePath": [{
                "id": { "module": "M", "namespace": ["Service"], "name": "Page.next" }, "name": "next"
              }],
              "type": { "kind": "primitive", "name": "string" }, "optional": true, "nullable": false,
              "bindings": [{
                "location": "header", "path": ["x-next.token"], "contentType": "application/json",
                "response": { "statusCode": { "start": 200, "end": 299, "kind": "range" } }
              }]
            }
            """;
        IROperationSemantics semantics = RoundTrip<IROperationSemantics>(
            $$"""
            {
              "pageable": {
                "isList": true, "metadataSource": "compiler",
                "itemType": { "kind": "primitive", "name": "string" },
                "input": { "offset": {{input}}, "pageIndex": {{input}}, "pageSize": {{input}}, "continuationToken": {{input}} },
                "output": {
                  "pageItems": {
                    "sourcePath": [{ "id": { "module": "M", "namespace": ["Service"], "name": "Page.items" }, "name": "items" }],
                    "type": { "kind": "array", "element": { "kind": "primitive", "name": "string" } },
                    "optional": false, "nullable": false,
                    "bindings": [{ "location": "body", "path": [], "contentType": "application/json",
                                   "response": { "statusCode": { "kind": "exact", "code": 200 } } }]
                  },
                  "nextLink": {{output}}, "prevLink": {{output}}, "firstLink": {{output}},
                  "lastLink": {{output}}, "continuationToken": {{output}}
                },
                "sdk": {
                  "disabled": true, "forced": false, "nextLinkVerb": "POST",
                  "nextLinkReinjectedParameters": [{{input}}],
                  "nextLinkOperation": { "module": "M", "namespace": ["Service"], "interface": "Lists", "name": "next" }
                }
              },
              "longRunning": { "pollingStrategy": "location", "finalStateVia": "originalUri",
                              "finalResultType": { "kind": "primitive", "name": "string" } }
            }
            """);

        IRPageableSemantics page = Assert.IsType<IRPageableSemantics>(semantics.Pageable);
        Assert.True(page.IsList);
        Assert.Equal(IRPageableSemanticsMetadataSource.Compiler, page.MetadataSource);
        Assert.All(new[] { page.Input.Offset, page.Input.PageIndex, page.Input.PageSize, page.Input.ContinuationToken }, property =>
        {
            Assert.NotNull(property);
            Assert.True(property.Optional);
            Assert.True(property.Nullable);
            Assert.Equal("Lists", Assert.Single(property.SourcePath).Id.Interface);
            IRPagingBinding binding = Assert.Single(property.Bindings);
            Assert.Equal(IRPagingBindingLocation.Query, binding.Location);
            Assert.Equal(["in.cursor"], binding.Path);
            Assert.Equal("list.cursor", binding.Parameter!.Name);
            Assert.Null(binding.Response);
        });
        Assert.IsType<IRTypeRefArray>(page.Output.PageItems.Type);
        Assert.Empty(Assert.Single(page.Output.PageItems.Bindings).Path);
        Assert.All(new[] { page.Output.NextLink, page.Output.PrevLink, page.Output.FirstLink, page.Output.LastLink, page.Output.ContinuationToken }, property =>
        {
            IRPagingBinding binding = Assert.Single(property!.Bindings);
            Assert.Equal(IRPagingBindingLocation.Header, binding.Location);
            Assert.Equal(["x-next.token"], binding.Path);
            Assert.Equal("application/json", binding.ContentType);
            Assert.Equal(299, Assert.IsType<IRStatusCodeRange>(binding.Response!.StatusCode).End);
        });
        Assert.True(page.Sdk.Disabled);
        Assert.Equal(IRPageableSemanticsSdkNextLinkVerb.POST, page.Sdk.NextLinkVerb);
        Assert.Single(page.Sdk.NextLinkReinjectedParameters);
        Assert.Equal("next", page.Sdk.NextLinkOperation!.Name);
        Assert.Equal(IRLongRunningSemanticsFinalStateVia.OriginalUri, semantics.LongRunning!.FinalStateVia);
        Assert.IsType<IRTypeRefPrimitive>(semantics.LongRunning.FinalResultType);
    }
}
