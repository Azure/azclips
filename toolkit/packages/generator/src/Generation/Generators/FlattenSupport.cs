using System.Text;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Shared support for emitting flattened members — the generator-side consumer of
/// <c>ModelHints.Flattened</c> and <c>RequestBodyHints.Parameters</c>.
/// </summary>
/// <remarks>
/// The transform already resolved naming and collisions, so nothing here re-derives a
/// name: this only resolves a member's <see cref="IRId"/> path back to the properties it
/// walks, and renders the access chain. The IR keeps wire truth on the properties
/// themselves, so a flattened member is always <c>[JsonIgnore]</c> sugar over the real
/// nested graph — never a replacement for it.
/// </remarks>
internal static class FlattenSupport
{
    /// <summary>Index every model property in the document by its serialized id.</summary>
    public static Dictionary<string, IRProperty> IndexProperties(IRDocument ir)
    {
        var index = new Dictionary<string, IRProperty>(StringComparer.Ordinal);
        foreach (IRModel model in ir.Models)
        {
            foreach (IRProperty property in model.Properties)
            {
                index[IrIds.Serialize(property.Id)] = property;
            }
        }

        return index;
    }

    /// <summary>
    /// Resolve a flattening path to the properties it walks, or <c>null</c> when a hop is
    /// unknown or cannot be walked (only a named model can sit between the root and the leaf).
    /// </summary>
    public static IRProperty[]? ResolvePath(
        IReadOnlyList<IRId>? path,
        IReadOnlyDictionary<string, IRProperty> index)
    {
        if (path is null || path.Count == 0)
        {
            return null;
        }

        var hops = new IRProperty[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            if (!index.TryGetValue(IrIds.Serialize(path[i]), out IRProperty? hop))
            {
                return null;
            }

            // Every hop before the leaf must be a materializable model instance.
            if (i < path.Count - 1 && hop is not IRPropertyObject { Type: IRTypeRefModel })
            {
                return null;
            }

            hops[i] = hop;
        }

        return hops;
    }

    /// <summary>The leaf's own C# type, carrying its own declared nullability.</summary>
    public static string DeclaredType(IRProperty leaf) =>
        IrTypeMapper.ToCSharp(leaf) + (IsNullable(leaf) ? "?" : "");

    /// <summary>
    /// The accessor's C# type: the leaf's type, widened to nullable when any hop along the
    /// path may be absent (so a <c>?.</c> chain can legitimately produce null).
    /// </summary>
    public static string AccessorType(IRProperty leaf, bool optional) =>
        IrTypeMapper.ToCSharp(leaf) + (optional || IsNullable(leaf) ? "?" : "");

    /// <summary>True when the accessor is nullable but the leaf it assigns to is not.</summary>
    public static bool WidensLeaf(IRProperty leaf, bool optional) => optional && !IsNullable(leaf);

    /// <summary>
    /// The value expression assigned at the end of a setter. When the accessor widened a
    /// non-nullable leaf (because an <em>ancestor</em> hop is optional), the value has to be
    /// narrowed again: a value type falls back to <c>default</c>, a reference type is
    /// null-forgiven.
    /// </summary>
    public static string AssignedValue(IRProperty leaf, bool optional)
    {
        if (!WidensLeaf(leaf, optional))
        {
            return "value";
        }

        return IsValueType(leaf) ? "value ?? default" : "value!";
    }

    /// <summary>
    /// The read chain from the containing type: <c>Properties?.Sku?.Name</c>. A hop is
    /// dereferenced with <c>?.</c> only when the preceding hop can be null.
    /// </summary>
    public static string GetterChain(IReadOnlyList<IRProperty> hops)
    {
        var chain = new StringBuilder(hops[0].Name.Identifier());
        bool nullable = IsNullable(hops[0]);
        for (int i = 1; i < hops.Count; i++)
        {
            chain.Append(nullable ? "?." : ".").Append(hops[i].Name.Identifier());
            nullable = IsNullable(hops[i]);
        }

        return chain.ToString();
    }

    /// <summary>
    /// The write chain: materialize each intermediate hop, then assign the leaf. Intermediates
    /// are always null-coalesced — a hop the spec marks required can still arrive null from
    /// deserialization, and assigning through it would otherwise throw.
    /// </summary>
    public static IEnumerable<string> SetterStatements(IReadOnlyList<IRProperty> hops, string assignedValue)
    {
        var chain = new StringBuilder();
        for (int i = 0; i < hops.Count - 1; i++)
        {
            if (i > 0)
            {
                chain.Append('.');
            }

            chain.Append(hops[i].Name.Identifier());
            yield return $"{chain} ??= new {IrTypeMapper.ToCSharp(hops[i])}();";
        }

        if (hops.Count > 1)
        {
            chain.Append('.');
        }

        chain.Append(hops[^1].Name.Identifier());
        yield return $"{chain} = {assignedValue};";
    }

    /// <summary>The JSON path a flattened member stands for, for its XML doc.</summary>
    public static string WirePath(IReadOnlyList<IRProperty> hops) =>
        string.Join(".", hops.Select(hop => hop.WireName));

    private static bool IsNullable(IRProperty property) => property.Optional || property.Nullable;

    private static bool IsValueType(IRProperty property) => property switch
    {
        IRPropertyPrimitive p => p.Type.Name is not (
            IRTypeRefPrimitiveName.String or
            IRTypeRefPrimitiveName.Bytes or
            IRTypeRefPrimitiveName.Any or
            IRTypeRefPrimitiveName.Void),
        IRPropertyEnum => true,
        _ => false,
    };
}
