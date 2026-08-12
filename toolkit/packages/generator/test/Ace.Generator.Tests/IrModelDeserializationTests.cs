using System.Text.Json;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Verifies the generated POCOs deserialize the IR's discriminated unions into the
/// right derived types via System.Text.Json polymorphism — the fidelity the
/// Option A schema refactor + Roslyn mapper exist to provide.
/// </summary>
public sealed class IrModelDeserializationTests
{
    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, IrJson.Options)!;

    [Fact]
    public void IRTypeRef_Primitive_DeserializesToDerivedType()
    {
        IRTypeRef result = Parse<IRTypeRef>("""{ "kind": "primitive", "name": "int64" }""");

        var primitive = Assert.IsType<IRTypeRefPrimitive>(result);
        Assert.Equal(IRTypeRefPrimitiveName.Int64, primitive.Name);
    }

    [Fact]
    public void IRTypeRef_Array_NestsPolymorphicElement()
    {
        IRTypeRef result = Parse<IRTypeRef>(
            """{ "kind": "array", "element": { "kind": "model", "id": { "module": "M", "namespace": [], "name": "Widget" } } }""");

        var array = Assert.IsType<IRTypeRefArray>(result);
        var model = Assert.IsType<IRTypeRefModel>(array.Element);
        Assert.Equal("Widget", model.Id.Name);
    }

    [Fact]
    public void IRProperty_DiscriminatesByTypeKind()
    {
        IRProperty result = Parse<IRProperty>(
            """
            {
              "typeKind": "primitive",
              "id": { "module": "M", "namespace": [], "name": "Widget.size" },
              "name": "Size",
              "wireName": "size",
              "optional": false,
              "nullable": false,
              "type": { "kind": "primitive", "name": "int32" }
            }
            """);

        var primitive = Assert.IsType<IRPropertyPrimitive>(result);
        Assert.Equal("Size", primitive.Name);
        Assert.Equal("size", primitive.WireName);
        Assert.Equal(IRTypeRefPrimitiveName.Int32, primitive.Type.Name);
    }

    [Fact]
    public void IRStatusCode_And_IRLiteralValue_Discriminate()
    {
        var status = Assert.IsType<IRStatusCodeExact>(Parse<IRStatusCode>("""{ "kind": "exact", "code": 200 }"""));
        Assert.Equal(200, status.Code);

        Assert.IsType<IRLiteralValueString>(Parse<IRLiteralValue>("""{ "kind": "string", "value": "hi" }"""));
        Assert.IsType<IRLiteralValueNull>(Parse<IRLiteralValue>("""{ "kind": "null" }"""));
    }

    [Fact]
    public void IROperationSemantics_DiscriminatesByKind()
    {
        IROperationSemantics result = Parse<IROperationSemantics>(
            """{ "kind": "longRunning", "pollingStrategy": "location", "finalStateVia": "location" }""");

        var lro = Assert.IsType<IROperationSemanticsLongRunning>(result);
        Assert.Equal(IROperationSemanticsLongRunningPollingStrategy.Location, lro.PollingStrategy);
    }

    [Fact]
    public void EnumMember_WithHyphen_MapsToWireValue()
    {
        // FormatKinds carry hyphens on the wire ("date-time") but sanitized C# names.
        IRTypeRef result = Parse<IRTypeRef>("""{ "kind": "primitive", "name": "string", "format": "date-time" }""");

        var primitive = Assert.IsType<IRTypeRefPrimitive>(result);
        Assert.Equal(IRTypeRefPrimitiveFormat.DateTime, primitive.Format);
    }
}
