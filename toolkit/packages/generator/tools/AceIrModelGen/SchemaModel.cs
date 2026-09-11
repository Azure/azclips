namespace Ace.Ir.CodeGen;

/// <summary>How a C# type is used, driving nullability and initializers.</summary>
internal enum TypeKind
{
    /// <summary>A C# value type (bool/int/double/enum): optional ⇒ <c>T?</c>.</summary>
    Value,

    /// <summary>A C# reference type (string/class): required ⇒ initialized <c>= default!</c>.</summary>
    Ref,

    /// <summary>A collection (<c>IReadOnlyList&lt;T&gt;</c>): required ⇒ initialized <c>= []</c>.</summary>
    Collection,
}

/// <summary>A resolved C# type reference for a property.</summary>
internal readonly record struct TypeRef(string Name, TypeKind Kind);

/// <summary>A generated C# enum.</summary>
internal sealed class EnumDecl
{
    public required string Name { get; init; }

    /// <summary>Members as (C# identifier, JSON wire value).</summary>
    public List<(string CsName, string WireValue)> Members { get; } = [];
}

/// <summary>
/// A generated C# class under construction. Field *schemas* are captured during
/// classification; they are resolved to <see cref="Props"/> in a later pass (which
/// may itself generate more inline types).
/// </summary>
internal sealed class ClassBuild
{
    public required string Name { get; init; }

    /// <summary>Base class name, when this type is a discriminated-union variant.</summary>
    public string? Base { get; set; }

    /// <summary>True for a polymorphic union base (emitted as <c>abstract</c>).</summary>
    public bool IsAbstract { get; set; }

    /// <summary>Discriminator JSON property name — set only on a union base.</summary>
    public string? DiscriminatorProperty { get; set; }

    /// <summary>Derived (type name, discriminator value) pairs — set only on a union base.</summary>
    public List<(string TypeName, string Value)> Derived { get; } = [];

    /// <summary>Input: raw field schemas (JSON name, schema, required).</summary>
    public List<(string JsonName, System.Text.Json.JsonElement Schema, bool Required)> Fields { get; } = [];

    /// <summary>Output: resolved properties (filled by the materialize pass).</summary>
    public List<PropDecl> Props { get; } = [];
}

/// <summary>A resolved C# property.</summary>
internal sealed record PropDecl(string JsonName, string CsName, TypeRef Type, bool Required);
