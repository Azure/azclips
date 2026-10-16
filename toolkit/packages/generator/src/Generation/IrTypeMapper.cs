using Ace.Generator.Generators;
using Ace.Generator.Model.Ir;

namespace Ace.Generator;

/// <summary>
/// Maps an <see cref="IRTypeRef"/> to a C# type expression. Primitives map to their
/// obvious CLR type, an enum renders as the primitive its values have, and structural
/// unions fall back to <c>object</c>.
/// </summary>
/// <remarks>
/// <para>
/// A model reference is resolved <em>through the document</em> rather than read off the
/// reference's own id. An <see cref="IRId"/> records where a declaration came from and
/// never changes; the rendered name can, because a transform may rename a declaration to
/// keep two same-named types apart. Reading `id.Name` here would then emit references to
/// a type nobody declares. Resolution falls back to the id's name when the document does
/// not contain the target, so a partial document still renders.
/// </para>
/// <para>
/// An enum is not a C# type: <see cref="Normalize"/> replaces its reference with the
/// primitive of its <see cref="IREnum.ValueType"/>, so a string enum renders as
/// <c>string</c> and an <c>int64</c> one as <c>long</c>. Its members stay in the IR for a
/// dialect to offer as completions. An enum the document does not declare has no known
/// value type and renders as <c>object</c>.
/// </para>
/// </remarks>
internal static class IrTypeMapper
{
    /// <summary>Render <paramref name="typeRef"/> as a C# type expression.</summary>
    public static string ToCSharp(IRTypeRef typeRef, IrIndex index) => Normalize(typeRef, index) switch
    {
        IRTypeRefPrimitive p => Primitive(p.Name),
        IRTypeRefModel m => (index.Model(m.Id)?.Name ?? m.Id.Name).Identifier(),
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

    /// <summary>
    /// <paramref name="typeRef"/> as it renders: a declared enum becomes the primitive of its
    /// value type; every other reference is returned unchanged.
    /// </summary>
    public static IRTypeRef Normalize(IRTypeRef typeRef, IrIndex index) =>
        typeRef is IRTypeRefEnum e && index.Enum(e.Id) is { } declared
            ? new IRTypeRefPrimitive { Name = PrimitiveName(declared.ValueType) }
            : typeRef;

    /// <summary>True when <paramref name="typeRef"/> renders as a C# value type.</summary>
    public static bool IsValueType(IRTypeRef typeRef, IrIndex index) =>
        Normalize(typeRef, index) is IRTypeRefPrimitive p && p.Name is not (
            IRTypeRefPrimitiveName.String or
            IRTypeRefPrimitiveName.Bytes or
            IRTypeRefPrimitiveName.Any or
            IRTypeRefPrimitiveName.Void);

    /// <summary>True when <paramref name="property"/> renders as a C# value type.</summary>
    public static bool IsValueType(IRProperty property, IrIndex index) => property switch
    {
        IRPropertyPrimitive p => IsValueType(p.Type, index),
        IRPropertyEnum p => IsValueType(p.Type, index),
        _ => false,
    };

    private static IRTypeRefPrimitiveName PrimitiveName(IREnumValueType valueType) => valueType switch
    {
        IREnumValueType.String => IRTypeRefPrimitiveName.String,
        IREnumValueType.Int8 => IRTypeRefPrimitiveName.Int8,
        IREnumValueType.Int16 => IRTypeRefPrimitiveName.Int16,
        IREnumValueType.Int32 => IRTypeRefPrimitiveName.Int32,
        IREnumValueType.Int64 => IRTypeRefPrimitiveName.Int64,
        IREnumValueType.Uint8 => IRTypeRefPrimitiveName.Uint8,
        IREnumValueType.Uint16 => IRTypeRefPrimitiveName.Uint16,
        IREnumValueType.Uint32 => IRTypeRefPrimitiveName.Uint32,
        IREnumValueType.Uint64 => IRTypeRefPrimitiveName.Uint64,
        IREnumValueType.Float32 => IRTypeRefPrimitiveName.Float32,
        IREnumValueType.Float64 => IRTypeRefPrimitiveName.Float64,
        _ => IRTypeRefPrimitiveName.Any,
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
