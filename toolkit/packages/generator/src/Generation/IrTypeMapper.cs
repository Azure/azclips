using Ace.Generator.Model.Ir;

namespace Ace.Generator;

/// <summary>
/// Maps an <see cref="IRTypeRef"/> to a C# type expression. Deliberately literal
/// ("garbage in, garbage out"): named model/enum references become their IR name
/// verbatim (no resolution or validation), primitives map to their obvious CLR
/// type, and structural unions fall back to <c>object</c>.
/// </summary>
internal static class IrTypeMapper
{
    /// <summary>Render <paramref name="typeRef"/> as a C# type expression.</summary>
    public static string ToCSharp(IRTypeRef typeRef) => typeRef switch
    {
        IRTypeRefPrimitive p => Primitive(p.Name),
        IRTypeRefModel m => m.Id.Name.Identifier(),
        IRTypeRefEnum e => e.Id.Name.Identifier(),
        IRTypeRefArray a => $"IList<{ToCSharp(a.Element)}>",
        IRTypeRefDictionary d => $"IDictionary<string, {ToCSharp(d.Value)}>",
        IRTypeRefUnion => "object",
        _ => "object",
    };

    /// <summary>The C# type of a property, dispatched over the property variant.</summary>
    public static string ToCSharp(IRProperty property) => property switch
    {
        IRPropertyPrimitive p => ToCSharp(p.Type),
        IRPropertyObject p => ToCSharp(p.Type),
        IRPropertyCollection p => ToCSharp(p.Type),
        IRPropertyEnum p => ToCSharp(p.Type),
        IRPropertyUnion p => ToCSharp(p.Type),
        _ => "object",
    };

    private static string Primitive(IRTypeRefPrimitiveName name) => name switch
    {
        IRTypeRefPrimitiveName.String => "string",
        IRTypeRefPrimitiveName.Boolean => "bool",
        IRTypeRefPrimitiveName.Int8 => "sbyte",
        IRTypeRefPrimitiveName.Int16 => "short",
        IRTypeRefPrimitiveName.Int32 => "int",
        IRTypeRefPrimitiveName.Int64 => "long",
        IRTypeRefPrimitiveName.Uint8 => "byte",
        IRTypeRefPrimitiveName.Uint16 => "ushort",
        IRTypeRefPrimitiveName.Uint32 => "uint",
        IRTypeRefPrimitiveName.Uint64 => "ulong",
        IRTypeRefPrimitiveName.Float32 => "float",
        IRTypeRefPrimitiveName.Float64 => "double",
        IRTypeRefPrimitiveName.Decimal => "decimal",
        IRTypeRefPrimitiveName.Decimal128 => "decimal",
        IRTypeRefPrimitiveName.Bytes => "byte[]",
        IRTypeRefPrimitiveName.Any => "object",
        IRTypeRefPrimitiveName.Void => "object",
        _ => "object",
    };
}
