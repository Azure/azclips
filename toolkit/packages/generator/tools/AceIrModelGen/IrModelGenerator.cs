using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Ace.Ir.CodeGen;

/// <summary>
/// Generates C# POCOs from the IR JSON Schema (itself derived from the Zod source
/// of truth). Strictly construct-driven: it switches on JSON Schema *shapes*
/// (object / <c>anyOf</c>+<c>const</c> / <c>enum</c> / <c>$ref</c> / array), never on
/// IR type *names*, so adding an IR type needs no change here as long as it uses a
/// shape already handled.
///
/// Discriminated unions (<c>anyOf</c> whose branches each carry a <c>const</c>
/// discriminator) become an <c>abstract</c> base + derived types wired for
/// System.Text.Json polymorphism (<c>[JsonPolymorphic]</c> / <c>[JsonDerivedType]</c>).
/// </summary>
internal sealed class IrModelGenerator
{
    private readonly JsonDocument _doc;
    private readonly JsonElement _root;
    private readonly JsonElement _defs;
    private readonly string _namespace;

    private readonly Dictionary<string, EnumDecl> _enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClassBuild> _classes = new(StringComparer.Ordinal);

    /// <summary>Names claimed as union-derived types (so they are not also emitted standalone).</summary>
    private readonly HashSet<string> _derivedNames = new(StringComparer.Ordinal);

    private const string RootTypeName = "TransformedIR";

    public IrModelGenerator(string schemaJson, string @namespace)
    {
        _doc = JsonDocument.Parse(schemaJson);
        _root = _doc.RootElement;
        _defs = _root.GetProperty("definitions");
        _namespace = @namespace;
    }

    public string Generate()
    {
        Classify();
        Materialize();
        return Emit();
    }

    // ── Classification ──────────────────────────────────────────────────

    private void Classify()
    {
        // Unions first, so ref-branch variants (e.g. IRTypeRefPrimitive) are marked
        // derived before we decide which object defs are standalone.
        foreach (JsonProperty def in _defs.EnumerateObject())
        {
            if (def.Value.TryGetProperty("anyOf", out _))
            {
                ClassifyUnion(def.Name, def.Value);
            }
        }

        foreach (JsonProperty def in _defs.EnumerateObject())
        {
            JsonElement d = def.Value;
            if (d.TryGetProperty("enum", out _) && IsString(d))
            {
                AddEnum(def.Name, d);
            }
            else if (d.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String
                     && t.GetString() == "object" && !_derivedNames.Contains(def.Name) && !_classes.ContainsKey(def.Name))
            {
                _classes[def.Name] = ObjectClass(def.Name, d);
            }
        }

        // The root schema (outside `definitions`) becomes TransformedIR: the emitted
        // root is `TransformedIRSchema`, so the generated document always carries
        // commands and a resolved target, never bare operations alone.
        _classes[RootTypeName] = ObjectClass(RootTypeName, _root);
    }

    private ClassBuild ObjectClass(string name, JsonElement obj)
    {
        var build = new ClassBuild { Name = name };
        HashSet<string> required = RequiredSet(obj);
        if (obj.TryGetProperty("properties", out JsonElement props))
        {
            foreach (JsonProperty p in props.EnumerateObject())
            {
                build.Fields.Add((p.Name, p.Value, required.Contains(p.Name)));
            }
        }

        return build;
    }

    private void ClassifyUnion(string name, JsonElement def)
    {
        List<(bool IsRef, string? RefName, JsonElement Obj)> branches = [];
        foreach (JsonElement b in def.GetProperty("anyOf").EnumerateArray())
        {
            if (b.TryGetProperty("$ref", out JsonElement r))
            {
                string ptr = r.GetString()!;
                branches.Add((true, ShallowRefName(ptr), ResolvePointer(ptr)));
            }
            else
            {
                branches.Add((false, null, b));
            }
        }

        string disc = FindDiscriminator(branches.Select(x => x.Obj));
        var baseBuild = new ClassBuild { Name = name, IsAbstract = true, DiscriminatorProperty = disc };
        _classes[name] = baseBuild;

        // Per-branch field maps (excluding the discriminator).
        var branchFields = new List<Dictionary<string, (JsonElement Schema, bool Required)>>();
        var branchNames = new List<(string DerivedName, string DiscValue)>();
        foreach ((bool isRef, string? refName, JsonElement obj) in branches)
        {
            JsonElement props = obj.GetProperty("properties");
            HashSet<string> required = RequiredSet(obj);
            string discValue = props.GetProperty(disc).GetProperty("const").GetString()!;
            string derivedName = isRef ? refName! : name + Pascal(discValue);
            branchNames.Add((derivedName, discValue));

            var map = new Dictionary<string, (JsonElement, bool)>(StringComparer.Ordinal);
            foreach (JsonProperty p in props.EnumerateObject())
            {
                if (p.Name == disc)
                {
                    continue;
                }

                map[p.Name] = (p.Value, required.Contains(p.Name));
            }

            branchFields.Add(map);
        }

        // Fields common to every branch (same resolved schema) are hoisted to the base.
        HashSet<string> shared = [];
        if (!branches.All(x => x.IsRef))
        {
            shared = branchFields.Select(f => f.Keys.ToHashSet(StringComparer.Ordinal))
                .Aggregate((a, b) => { a.IntersectWith(b); return a; });
            shared = shared
                .Where(k => branchFields.All(f => SchemaSignature(f[k].Item1) == SchemaSignature(branchFields[0][k].Item1)))
                .ToHashSet(StringComparer.Ordinal);
        }

        foreach (string k in shared)
        {
            baseBuild.Fields.Add((k, branchFields[0][k].Item1, branchFields.All(f => f[k].Item2)));
        }

        for (int i = 0; i < branches.Count; i++)
        {
            (string derivedName, string discValue) = branchNames[i];
            baseBuild.Derived.Add((derivedName, discValue));
            _derivedNames.Add(derivedName);

            if (!_classes.TryGetValue(derivedName, out ClassBuild? db))
            {
                db = new ClassBuild { Name = derivedName };
                _classes[derivedName] = db;
            }

            db.Base = name;
            foreach ((string fieldName, (JsonElement schema, bool req)) in branchFields[i])
            {
                if (!shared.Contains(fieldName))
                {
                    db.Fields.Add((fieldName, schema, req));
                }
            }
        }
    }

    private void AddEnum(string name, JsonElement schema)
    {
        if (_enums.ContainsKey(name))
        {
            return;
        }

        var e = new EnumDecl { Name = name };
        foreach (JsonElement v in schema.GetProperty("enum").EnumerateArray())
        {
            string wire = v.GetString()!;
            e.Members.Add((SanitizeIdentifier(Pascal(wire)), wire));
        }

        _enums[name] = e;
    }

    // ── Materialization (field schemas → resolved properties) ───────────

    private void Materialize()
    {
        var queue = new Queue<ClassBuild>(_classes.Values);
        while (queue.Count > 0)
        {
            ClassBuild build = queue.Dequeue();
            if (build.Props.Count > 0 || build.Fields.Count == 0)
            {
                continue; // already materialized, or an empty variant
            }

            foreach ((string jsonName, JsonElement schema, bool required) in build.Fields)
            {
                int before = _classes.Count;
                TypeRef type = MapType(schema, build.Name, jsonName);
                build.Props.Add(new PropDecl(jsonName, SanitizeIdentifier(Pascal(jsonName)), type, required));

                // Inline object types created during mapping must also be materialized.
                if (_classes.Count > before)
                {
                    foreach (ClassBuild added in _classes.Values.Where(c => c.Props.Count == 0 && c.Fields.Count > 0))
                    {
                        queue.Enqueue(added);
                    }
                }
            }
        }
    }

    private TypeRef MapType(JsonElement schema, string owner, string prop)
    {
        if (schema.TryGetProperty("$ref", out JsonElement r))
        {
            string ptr = r.GetString()!;
            string? shallow = ShallowRefName(ptr);
            return shallow is not null ? NamedRef(shallow) : MapType(ResolvePointer(ptr), owner, prop);
        }

        if (schema.TryGetProperty("enum", out _) && IsString(schema))
        {
            string enumName = owner + Pascal(prop);
            AddEnum(enumName, schema);
            return new TypeRef(enumName, TypeKind.Value);
        }

        if (schema.TryGetProperty("anyOf", out JsonElement anyOf))
        {
            return MapInlineUnion(anyOf);
        }

        if (schema.TryGetProperty("type", out JsonElement type))
        {
            if (type.ValueKind == JsonValueKind.Array)
            {
                return new TypeRef("object", TypeKind.Ref); // e.g. string|number
            }

            switch (type.GetString())
            {
                case "array":
                    TypeRef element = MapType(schema.GetProperty("items"), owner, prop);
                    return new TypeRef($"IReadOnlyList<{element.Name}>", TypeKind.Collection);
                case "object" when schema.TryGetProperty("properties", out _):
                    string nested = owner + Pascal(prop);
                    if (!_classes.ContainsKey(nested))
                    {
                        _classes[nested] = ObjectClass(nested, schema);
                    }

                    return new TypeRef(nested, TypeKind.Ref);
                case "string":
                    return new TypeRef("string", TypeKind.Ref);
                case "boolean":
                    return new TypeRef("bool", TypeKind.Value);
                case "integer":
                    return new TypeRef("int", TypeKind.Value);
                case "number":
                    return new TypeRef("double", TypeKind.Value);
            }
        }

        return new TypeRef("object", TypeKind.Ref);
    }

    /// <summary>Map an inline <c>anyOf</c> of refs to their single common base, else <c>object</c>.</summary>
    private TypeRef MapInlineUnion(JsonElement anyOf)
    {
        var bases = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement b in anyOf.EnumerateArray())
        {
            if (!b.TryGetProperty("$ref", out JsonElement r) || ShallowRefName(r.GetString()!) is not string name
                || !_classes.TryGetValue(name, out ClassBuild? cb) || cb.Base is null)
            {
                return new TypeRef("object", TypeKind.Ref);
            }

            bases.Add(cb.Base);
        }

        return bases.Count == 1 ? NamedRef(bases.First()) : new TypeRef("object", TypeKind.Ref);
    }

    private TypeRef NamedRef(string name) =>
        new(name, _enums.ContainsKey(name) ? TypeKind.Value : TypeKind.Ref);

    // ── Emission (Roslyn) ───────────────────────────────────────────────

    private string Emit()
    {
        var members = new List<MemberDeclarationSyntax>();

        foreach (EnumDecl e in _enums.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            members.Add(EmitEnum(e));
        }

        foreach (ClassBuild c in _classes.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            members.Add(EmitClass(c));
        }

        CompilationUnitSyntax unit = CompilationUnit()
            .AddUsings(
                UsingDirective(ParseName("System.Collections.Generic")),
                UsingDirective(ParseName("System.Text.Json.Serialization")))
            .AddMembers(FileScopedNamespaceDeclaration(ParseName(_namespace)).AddMembers([.. members]))
            .NormalizeWhitespace();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     Generated by AceIrModelGen from the IR JSON Schema (the Zod source of truth).");
        sb.AppendLine("//     Do not edit by hand; re-run the generator instead.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.Append(unit.ToFullString());
        sb.AppendLine();
        return sb.ToString();
    }

    private EnumMemberDeclarationSyntax EmitEnumMember((string CsName, string WireValue) m) =>
        EnumMemberDeclaration(Identifier(m.CsName))
            .WithAttributeLists(SingletonList(AttrList(Attr("JsonStringEnumMemberName", StringArg(m.WireValue)))));

    private EnumDeclarationSyntax EmitEnum(EnumDecl e) =>
        EnumDeclaration(e.Name)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithAttributeLists(SingletonList(AttrList(Attr("JsonConverter", TypeOfArg("JsonStringEnumConverter")))))
            .AddMembers([.. e.Members.Select(EmitEnumMember)]);

    private ClassDeclarationSyntax EmitClass(ClassBuild c)
    {
        ClassDeclarationSyntax decl = ClassDeclaration(c.Name).AddModifiers(Token(SyntaxKind.PublicKeyword));
        decl = c.IsAbstract
            ? decl.AddModifiers(Token(SyntaxKind.AbstractKeyword), Token(SyntaxKind.PartialKeyword))
            : decl.AddModifiers(Token(SyntaxKind.PartialKeyword));

        if (c.Base is not null)
        {
            decl = decl.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(ParseTypeName(c.Base)))));
        }

        if (c.DiscriminatorProperty is not null)
        {
            var attrs = new List<AttributeSyntax>
            {
                Attr("JsonPolymorphic", NamedStringArg("TypeDiscriminatorPropertyName", c.DiscriminatorProperty)),
            };
            foreach ((string typeName, string value) in c.Derived)
            {
                attrs.Add(Attr("JsonDerivedType", TypeOfArg(typeName), StringArg(value)));
            }

            decl = decl.WithAttributeLists(List(attrs.Select(a => AttrList(a))));
        }

        return decl.AddMembers([.. c.Props.Select(EmitProperty)]);
    }

    private PropertyDeclarationSyntax EmitProperty(PropDecl p)
    {
        bool nullable = !p.Required;
        string typeName = nullable ? p.Type.Name + "?" : p.Type.Name;

        PropertyDeclarationSyntax decl = PropertyDeclaration(ParseTypeName(typeName), Identifier(p.CsName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithAttributeLists(SingletonList(AttrList(Attr("JsonPropertyName", StringArg(p.JsonName)))))
            .WithAccessorList(AccessorList(List(
            [
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            ])));

        // Initializers only for required members, to satisfy NRT without forcing `required`.
        if (p.Required)
        {
            string? init = p.Type.Kind switch
            {
                TypeKind.Collection => "[]",
                TypeKind.Ref => "default!",
                _ => null,
            };
            if (init is not null)
            {
                decl = decl
                    .WithInitializer(EqualsValueClause(ParseExpression(init)))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
        }

        return decl;
    }

    // ── Attribute helpers ───────────────────────────────────────────────

    private static AttributeListSyntax AttrList(AttributeSyntax a) => AttributeList(SingletonSeparatedList(a));

    private static AttributeSyntax Attr(string name, params AttributeArgumentSyntax[] args) =>
        args.Length == 0
            ? Attribute(ParseName(name))
            : Attribute(ParseName(name)).WithArgumentList(AttributeArgumentList(SeparatedList(args)));

    private static AttributeArgumentSyntax StringArg(string value) =>
        AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value)));

    private static AttributeArgumentSyntax TypeOfArg(string typeName) =>
        AttributeArgument(TypeOfExpression(ParseTypeName(typeName)));

    private static AttributeArgumentSyntax NamedStringArg(string name, string value) =>
        StringArg(value).WithNameEquals(NameEquals(IdentifierName(name)));

    // ── Schema helpers ──────────────────────────────────────────────────

    private static bool IsString(JsonElement schema) =>
        schema.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String && t.GetString() == "string";

    private static HashSet<string> RequiredSet(JsonElement obj) =>
        obj.TryGetProperty("required", out JsonElement req)
            ? req.EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];

    private static string FindDiscriminator(IEnumerable<JsonElement> objects)
    {
        List<JsonElement> objs = objects.ToList();
        foreach (JsonProperty p in objs[0].GetProperty("properties").EnumerateObject())
        {
            bool constInAll = objs.All(o =>
                o.GetProperty("properties").TryGetProperty(p.Name, out JsonElement pv) && pv.TryGetProperty("const", out _));
            if (constInAll)
            {
                return p.Name;
            }
        }

        throw new InvalidOperationException("anyOf union has no shared const discriminator property.");
    }

    /// <summary>Name for a shallow <c>#/definitions/&lt;Name&gt;</c> ref, else null (deep pointer).</summary>
    private static string? ShallowRefName(string pointer)
    {
        string[] parts = pointer.Split('/');
        return parts.Length == 3 && parts[0] == "#" && parts[1] == "definitions" ? parts[2] : null;
    }

    private JsonElement ResolvePointer(string pointer)
    {
        JsonElement current = _root;
        foreach (string raw in pointer.TrimStart('#', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string key = raw.Replace("~1", "/").Replace("~0", "~");
            current = current.ValueKind == JsonValueKind.Array ? current[int.Parse(key)] : current.GetProperty(key);
        }

        return current;
    }

    /// <summary>Canonical signature of a schema for equality, resolving refs so deep-ref'd shared fields compare equal.</summary>
    private string SchemaSignature(JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out JsonElement r))
        {
            string ptr = r.GetString()!;
            return ShallowRefName(ptr) is string name ? "ref:" + name : SchemaSignature(ResolvePointer(ptr));
        }

        return schema.GetRawText();
    }

    // ── Identifier helpers ──────────────────────────────────────────────

    private static string Pascal(string value)
    {
        string[] parts = value.Split(['-', '_', '.', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "_";
        }

        var sb = new StringBuilder();
        foreach (string p in parts)
        {
            sb.Append(char.ToUpperInvariant(p[0])).Append(p, 1, p.Length - 1);
        }

        return sb.ToString();
    }

    private static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char ch in name)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        string result = sb.ToString();
        if (result.Length == 0 || (!char.IsLetter(result[0]) && result[0] != '_'))
        {
            result = "_" + result;
        }

        return SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None
            ? "@" + result
            : result;
    }
}
