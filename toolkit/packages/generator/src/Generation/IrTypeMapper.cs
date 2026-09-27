using Ace.Generator.Generators;
using Ace.Generator.Model.Ir;

namespace Ace.Generator;

/// <summary>
/// Maps an <see cref="IRTypeRef"/> to a C# type expression. Primitives map to their
/// obvious CLR type and structural unions fall back to <c>object</c>.
/// </summary>
/// <remarks>
/// A model or enum reference is resolved <em>through the document</em> rather than
/// read off the reference's own id. An <see cref="IRId"/> records where a declaration
/// came from and never changes; the rendered name can, because a transform may rename
/// a declaration to keep two same-named types apart. Reading `id.Name` here would then
/// emit references to a type nobody declares. Resolution falls back to the id's name
/// when the document does not contain the target, so a partial document still renders.
/// </remarks>
internal static class IrTypeMapper
{
    /// <summary>Render <paramref name="typeRef"/> as a C# type expression.</summary>
    public static string ToCSharp(IRTypeRef typeRef, IrIndex index) => typeRef switch
    {
        IRTypeRefPrimitive p => Primitive(p.Name),
        IRTypeRefModel m => (index.Model(m.Id)?.Name ?? m.Id.Name).Identifier(),
        IRTypeRefEnum e => (index.Enum(e.Id)?.Name ?? e.Id.Name).Identifier(),
        IRTypeRefArray a => $"IList<{ToCSharp(a.Element, index)}>",
        IRTypeRefDictionary d => $"IDictionary<string, {ToCSharp(d.Value, index)}>",
        IRTypeRefUnion => "object",
        _ => "object",
    };

    /// <summary>The C# type of a property, dispatched over the property variant.</summary>
    public static string ToCSharp(IRProperty property, IrIndex index) => property switch
    {
        IRPropertyPrimitive p => ToCSharp(p.Type, index),
        IRPropertyObject p => ToCSharp(p.Type, index),
        IRPropertyCollection p => ToCSharp(p.Type, index),
        IRPropertyEnum p => ToCSharp(p.Type, index),
        IRPropertyUnion p => ToCSharp(p.Type, index),
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
