using System.Text.Json;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class MultipartIrTests
{
    private const string MultipartJson = """
        {
          "kind": "tuple",
          "contentTypes": ["multipart/mixed"],
          "parts": [
            {
              "name": "documents",
              "optional": true,
              "multi": true,
              "filename": {
                "name": "Filename", "type": { "kind": "primitive", "name": "string" },
                "optional": true, "nullable": false
              },
              "headers": [
                {
                  "name": "Checksum", "wireName": "x-checksum",
                  "type": { "kind": "primitive", "name": "string" },
                  "optional": false, "nullable": false
                }
              ],
              "body": {
                "type": { "kind": "model", "id": { "module": "M", "namespace": [], "name": "Document" } },
                "nullable": false,
                "contentTypes": ["application/pdf"],
                "contentType": {
                  "name": "ContentType", "type": { "kind": "primitive", "name": "string" },
                  "optional": false, "nullable": false,
                  "defaultValue": { "kind": "string", "value": "application/pdf" }
                },
                "contents": {
                  "name": "Contents", "type": { "kind": "primitive", "name": "bytes" },
                  "optional": false, "nullable": false
                },
                "isText": false,
                "kind": "file"
              }
            },
            {
              "optional": false, "multi": false, "headers": [],
              "body": {
                "type": { "kind": "primitive", "name": "string" },
                "nullable": false, "contentTypes": ["text/plain"], "kind": "single"
              }
            }
          ]
        }
        """;

    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, IrJson.Options)!;

    [Fact]
    public void Tuple_Request_RoundTrips_Ordered_Parts_And_File_Metadata()
    {
        var body = Parse<IRRequestBody>(
            $$"""{"contentType":"multipart/mixed","multipart":{{MultipartJson}}}""");
        var roundTripped = Parse<IRRequestBody>(JsonSerializer.Serialize(body, IrJson.Options));

        Assert.Null(roundTripped.Type);
        IRMultipartBody multipart = Assert.IsType<IRMultipartBody>(roundTripped.Multipart);
        Assert.Equal(IRMultipartBodyKind.Tuple, multipart.Kind);
        Assert.Equal(["multipart/mixed"], multipart.ContentTypes);
        Assert.Equal(2, multipart.Parts.Count);
        IRMultipartPart part = multipart.Parts[0];
        Assert.Equal("documents", part.Name);
        Assert.True(part.Optional);
        Assert.True(part.Multi);
        Assert.Null(part.Property);
        Assert.Equal("Filename", part.Filename!.Name);
        Assert.True(part.Filename.Optional);
        Assert.Equal("x-checksum", Assert.Single(part.Headers).WireName);
        var file = Assert.IsType<IRMultipartPartBodyFile>(part.Body);
        Assert.False(file.IsText);
        Assert.Equal("Document", Assert.IsType<IRTypeRefModel>(file.Type).Id.Name);
        Assert.Equal("application/pdf", Assert.IsType<IRLiteralValueString>(file.ContentType!.DefaultValue).Value);
        var contents = Assert.IsType<IRTypeRefPrimitive>(file.Contents.Type);
        Assert.Equal(IRTypeRefPrimitiveName.Bytes, contents.Name);
        Assert.Null(contents.Format);
        Assert.Null(multipart.Parts[1].Name);
        Assert.IsType<IRMultipartPartBodySingle>(multipart.Parts[1].Body);
    }

    [Fact]
    public void Response_Keeps_Outer_Headers_Separate_From_Part_Headers()
    {
        var response = Parse<IRResponse>(
            $$"""
              {
                "statusCode": { "kind": "exact", "code": 200 },
                "contentType": "multipart/mixed",
                "multipart": {{MultipartJson}},
                "headers": [{ "name": "RequestId", "wireName": "x-request-id",
                              "type": { "kind": "primitive", "name": "string" } }],
                "isError": false
              }
              """);
        var roundTripped = Parse<IRResponse>(JsonSerializer.Serialize(response, IrJson.Options));

        Assert.Null(roundTripped.Type);
        Assert.Equal("x-request-id", Assert.Single(roundTripped.Headers!).WireName);
        Assert.Equal("x-checksum", Assert.Single(roundTripped.Multipart!.Parts[0].Headers).WireName);
        Assert.IsType<IRMultipartPartBodyFile>(roundTripped.Multipart.Parts[0].Body);
    }

    [Fact]
    public void Model_Part_Property_Reference_RoundTrips()
    {
        var part = Parse<IRMultipartPart>(
            """
            {
              "name": "mime-name",
              "property": { "module": "M", "namespace": ["Service"], "name": "Form.Upload" },
              "optional": false, "multi": false, "headers": [],
              "body": { "kind": "single", "type": { "kind": "primitive", "name": "bytes" },
                        "nullable": false, "contentTypes": ["application/octet-stream"] }
            }
            """);
        var roundTripped = Parse<IRMultipartPart>(JsonSerializer.Serialize(part, IrJson.Options));

        Assert.Equal("mime-name", roundTripped.Name);
        Assert.Equal("Form.Upload", roundTripped.Property!.Name);
        Assert.Equal(["Service"], roundTripped.Property.Namespace);
    }
}
