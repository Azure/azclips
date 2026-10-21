using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Covers the generator's consumption of the flattening metadata a transform resolves:
/// <c>ModelHints.Flattened</c> becomes <c>[JsonIgnore]</c> accessors over the nested graph,
/// and <c>RequestBodyHints.Parameters</c> becomes flat processor members that delegate to them.
/// </summary>
public sealed class FlatteningTests
{
    private const string Module = "M";

    private static IRId Id(string name) => new() { Module = Module, Namespace = ["S"], Name = name };

    private static IRProperty Prim(string owner, string name, bool optional = true, bool int64 = false) =>
        new IRPropertyPrimitive
        {
            Id = Id($"{owner}.{char.ToLowerInvariant(name[0])}{name[1..]}"),
            Name = name,
            WireName = char.ToLowerInvariant(name[0]) + name[1..],
            Optional = optional,
            Type = new IRTypeRefPrimitive
            {
                Name = int64 ? IRTypeRefPrimitiveName.Int64 : IRTypeRefPrimitiveName.String,
            },
        };

    private static IRProperty Obj(string owner, string name, string target, bool optional = true, bool hoisted = false) =>
        new IRPropertyObject
        {
            Id = Id($"{owner}.{char.ToLowerInvariant(name[0])}{name[1..]}"),
            Name = name,
            WireName = char.ToLowerInvariant(name[0]) + name[1..],
            Optional = optional,
            Type = new IRTypeRefModel { Id = Id(target) },
            // What the transform stamps on a container whose members it hoisted.
            Rendering = hoisted
                ? new ObjectPropertyHints { Flatten = new FlattenHint { Into = FlattenHintInto.Self } }
                : null,
        };

    /// <summary>A required property typed by the enum named <paramref name="enumName"/>.</summary>
    private static IRProperty EnumLeaf(string owner, string name, string enumName) =>
        new IRPropertyEnum
        {
            Id = Id($"{owner}.{char.ToLowerInvariant(name[0])}{name[1..]}"),
            Name = name,
            WireName = char.ToLowerInvariant(name[0]) + name[1..],
            Optional = false,
            Type = new IRTypeRefEnum { Id = Id(enumName) },
        };

    private static IREnum EnumOf(string name, IREnumValueType valueType, params object[] values) =>
        new()
        {
            Id = Id(name),
            Name = name,
            ValueType = valueType,
            IsFixed = true,
            Source = Module,
            Members = [.. values.Select(value => new IREnumMember { Name = $"{value}", Value = value })],
        };

    private static IRModel Model(string name, params IRProperty[] properties) =>
        new() { Id = Id(name), Name = name, Source = Module, Properties = properties };

    private static FlattenedMember Member(string name, bool optional, IRId property, params IRId[] via) =>
        new() { Name = name, Property = property, Via = via, Optional = optional };

    /// <summary>A member no hop exposes for create or update — server-owned.</summary>
    private static FlattenedMember ReadOnly(string name, bool optional, IRId property, params IRId[] via) =>
        new() { Name = name, Property = property, Via = via, Optional = optional, Visibility = [FlattenedMemberVisibility.Read] };

    /// <summary>A member whose hops intersect on a write phase, so it stays settable.</summary>
    private static FlattenedMember Writable(string name, bool optional, IRId property, params IRId[] via) =>
        new()
        {
            Name = name,
            Property = property,
            Via = via,
            Optional = optional,
            Visibility = [FlattenedMemberVisibility.Read, FlattenedMemberVisibility.Update],
        };

    private static IRId ServerId() => Id("server");

    /// <summary>Thing { properties -&gt; ThingProperties { size, sku -&gt; Sku { name } } }.</summary>
    private static TransformedIR Document(bool withOperation = false, bool hoisted = false)
    {
        IRModel thing = Model("Thing", Obj("Thing", "Properties", "ThingProperties", optional: false, hoisted: hoisted));
        thing.Rendering = new ModelHints
        {
            Flattened =
            [
                // Required the whole way down: a plain `.` chain and a non-nullable accessor.
                Member("Size", false, Id("ThingProperties.size"), via: [Id("Thing.properties")]),
                // Crosses an optional hop, so it widens to nullable.
                Member("SkuName", true, Id("Sku.name"), via: [Id("Thing.properties"), Id("ThingProperties.sku")]),
            ],
        };

        IRModel thingProperties = Model(
            "ThingProperties",
            Prim("ThingProperties", "Size", optional: false, int64: true),
            Obj("ThingProperties", "Sku", "Sku"));
        thingProperties.Rendering = new ModelHints
        {
            Flattened = [Member("SkuName", true, Id("Sku.name"), via: [Id("ThingProperties.sku")])],
        };

        var ir = new TransformedIR
        {
            SchemaVersion = "0.17.0",
            Sources = [new IRSource { Id = Module, Server = ServerId() }],
            Servers = [new IRServerDefinition { Id = ServerId(), Endpoint = new IRServerLiteral { Url = "https://example.com" } }],
            Models = [thing, thingProperties, Model("Sku", Prim("Sku", "Name", optional: false))],
            Operations = [],
            Commands = [],
            Target = Targets.For("Contoso.Clients"),
        };

        if (withOperation)
        {
            ir.Operations = [Operation()];
            ir.Commands = [Command()];
        }

        return ir;
    }

    /// <summary>
    /// The projected command for <see cref="Operation()"/>: a PUT whose body is covered by flat
    /// members, so the generator hides <c>Body</c> and emits views over it.
    /// </summary>
    private static IRCommand Command(IReadOnlyList<IRCommandParameter>? parameters = null) =>
        Commands.Atomic(
            "Create",
            ServerId(),
            Id("create"),
            method: IRCommandRequestMethod.PUT,
            path: [Commands.Literal("/things")],
            body: Commands.Body(Id("Thing"), projected: true),
            surface: Commands.Surface(request: new IRTypeRefModel { Id = Id("Thing") }),
            parameters: parameters ??
            [
                Commands.Parameter(
                    "Size",
                    Commands.FromBody(Id("ThingProperties.size"), via: [Id("Thing.properties")]),
                    new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.Int64 }),
                Commands.Parameter(
                    "SkuName",
                    Commands.FromBody(Id("Sku.name"), via: [Id("Thing.properties"), Id("ThingProperties.sku")]),
                    required: false),
            ]);

    private static IROperation Operation(IReadOnlyList<FlattenedParameter>? parameters = null) => new()
    {
        Id = Id("create"),
        Name = "create",
        Source = Module,
        Server = ServerId(),
        Method = IROperationMethod.PUT,
        PathTemplate = "/things",
        RequestBody = new IRRequestBody
        {
            Type = new IRTypeRefModel { Id = Id("Thing") },
            ContentType = "application/json",
            Rendering = new RequestBodyHints
            {
                Flatten = new FlattenHint { Into = FlattenHintInto.Self },
                Parameters = parameters ??
                [
                    new FlattenedParameter { Name = "Size", Property = Id("ThingProperties.size"), Via = [Id("Thing.properties")], Required = true },
                    new FlattenedParameter { Name = "SkuName", Property = Id("Sku.name"), Via = [Id("Thing.properties"), Id("ThingProperties.sku")], Required = false },
                ],
            },
        },
    };

    private static async Task<(GenerationResult Result, GeneratedFileCollector Files)> RunAsync(TransformedIR ir)
    {
        var files = new GeneratedFileCollector();
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir, config: null, files.WriteAsync);
        return (result, files);
    }

    private static void AssertParses(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        Assert.DoesNotContain(
            tree.GetDiagnostics(),
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Flattened_Members_Are_JsonIgnored_Accessors_Beside_The_Real_Properties()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document());

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        string thing = files.Files.Source(result, "Thing");

        // The nested property still carries the wire shape — flattening is additive.
        Assert.Contains("[JsonPropertyName(\"properties\")]", thing);
        Assert.Contains("public ThingProperties Properties", thing);

        // The accessor must not serialize, or the value would appear at two depths.
        Assert.Contains("[JsonIgnore]", thing);
        AssertParses(thing);
    }

    [Fact]
    public async Task A_Required_Path_Reads_Directly_And_An_Optional_Hop_Widens_To_Nullable()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document());
        string thing = files.Files.Source(result, "Thing");

        // Every hop required: no null-conditional, and the accessor keeps the leaf's type.
        Assert.Contains("public long Size", thing);
        Assert.Contains("get => Properties.Size;", thing);

        // `Sku` is optional, so reading can produce null and the accessor widens.
        Assert.Contains("public string? SkuName", thing);
        Assert.Contains("get => Properties.Sku?.Name;", thing);
    }

    [Fact]
    public async Task Setters_Materialize_Every_Intermediate_Hop_Before_Assigning()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document());
        string thing = files.Files.Source(result, "Thing");

        Assert.Contains("Properties ??= new ThingProperties();", thing);
        Assert.Contains("Properties.Sku ??= new Sku();", thing);
        // The leaf is non-nullable but the accessor widened, so the value is narrowed again.
        Assert.Contains("Properties.Sku.Name = value!;", thing);
    }

    [Fact]
    public async Task Projected_Body_Parameters_Become_Flat_Members_Delegating_To_The_Model()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document(withOperation: true));

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        string processor = files.Files.Source(result, "CreateProcessor");

        // Body is internal now that flat members cover it — that is what removes the duplicate,
        // since argument binding and help both reflect over public properties only.
        Assert.Contains("internal Thing? Body", processor);
        Assert.DoesNotContain("public Thing? Body", processor);
        Assert.Contains("protected override Thing? CreateRequestBody() => Body;", processor);

        // Each flat member delegates to the model's own member rather than re-walking the path.
        Assert.Contains("public long? Size { get => Body?.Size; set => (Body ??= new Thing()).Size = value ?? default; }", processor);
        Assert.Contains("public string? SkuName { get => Body?.SkuName; set => (Body ??= new Thing()).SkuName = value; }", processor);
        AssertParses(processor);
    }

    [Fact]
    public async Task An_Enum_Leaf_Renders_And_Narrows_As_Its_Value_Type()
    {
        // The leaves become enums of their own value types, so nothing about the output may
        // change: a numeric enum falls back to `default`, a string enum is null-forgiven.
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(EnumLeafDocument());

        Assert.Empty(result.Diagnostics);
        string thing = files.Files.Source(result, "Thing");
        Assert.Contains("public long Size", thing);
        Assert.Contains("public string? SkuName", thing);
        Assert.Contains("Properties.Sku.Name = value!;", thing);
        string processor = files.Files.Source(result, "CreateProcessor");
        Assert.Contains("public long? Size { get => Body?.Size; set => (Body ??= new Thing()).Size = value ?? default; }", processor);
        Assert.Contains("public string? SkuName { get => Body?.SkuName; set => (Body ??= new Thing()).SkuName = value!; }", processor);
        AssertParses(thing);
        AssertParses(processor);
    }

    [Fact]
    public async Task An_Enum_Leaf_Lists_Its_Values_On_The_Accessor_And_The_Flat_Member()
    {
        // Both stand in for the leaf, so each documents the values it takes, as the leaf does.
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(EnumLeafDocument());

        Assert.Empty(result.Diagnostics);
        string thing = files.Files.Source(result, "Thing");
        Assert.Contains("/// <remarks>Allowed values (fixed enum SizeKind): 1, 2.</remarks>", thing);
        Assert.Contains("/// <remarks>Allowed values (fixed enum SkuTier): \"basic\".</remarks>", thing);
        string processor = files.Files.Source(result, "CreateProcessor");
        Assert.Contains("/// <remarks>Allowed values (fixed enum SizeKind): 1, 2.</remarks>", processor);
        Assert.Contains("/// <remarks>Allowed values (fixed enum SkuTier): \"basic\".</remarks>", processor);
        AssertParses(thing);
        AssertParses(processor);
    }

    /// <summary>
    /// <see cref="Document"/> with its operation, the <c>Size</c> and <c>Sku.Name</c> leaves typed
    /// by fixed enums, numeric and string.
    /// </summary>
    private static TransformedIR EnumLeafDocument()
    {
        TransformedIR ir = Document(withOperation: true);
        ir.Enums = [EnumOf("SizeKind", IREnumValueType.Int64, 1L, 2L), EnumOf("SkuTier", IREnumValueType.String, "basic")];
        IRModel thingProperties = ir.Models.Single(m => m.Name == "ThingProperties");
        thingProperties.Properties = [EnumLeaf("ThingProperties", "Size", "SizeKind"), thingProperties.Properties[1]];
        ir.Models.Single(m => m.Name == "Sku").Properties = [EnumLeaf("Sku", "Name", "SkuTier")];
        ir.Commands =
        [
            Command([
                Commands.Parameter(
                    "Size",
                    Commands.FromBody(Id("ThingProperties.size"), via: [Id("Thing.properties")]),
                    new IRTypeRefEnum { Id = Id("SizeKind") }),
                Commands.Parameter(
                    "SkuName",
                    Commands.FromBody(Id("Sku.name"), via: [Id("Thing.properties"), Id("ThingProperties.sku")]),
                    new IRTypeRefEnum { Id = Id("SkuTier") }),
            ]),
        ];

        return ir;
    }

    [Fact]
    public async Task A_Renamed_Body_Parameter_Still_Resolves_To_Its_Model_Member_By_Id()
    {
        TransformedIR ir = Document();
        // The transform renames a body parameter when it collides with an operation
        // parameter, so the name must not be how the generator finds the model member.
        ir.Operations =
        [
            Operation([
                new FlattenedParameter
                {
                    Name = "ThingSkuName",
                    Property = Id("Sku.name"),
                    Via = [Id("Thing.properties"), Id("ThingProperties.sku")],
                    Required = false,
                },
            ]),
        ];
        ir.Commands =
        [
            Command([
                Commands.Parameter(
                    "ThingSkuName",
                    Commands.FromBody(Id("Sku.name"), via: [Id("Thing.properties"), Id("ThingProperties.sku")]),
                    required: false),
            ]),
        ];

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        Assert.Empty(result.Diagnostics);
        string processor = files.Files.Source(result, "CreateProcessor");
        Assert.Contains("public string? ThingSkuName { get => Body?.SkuName;", processor);
        Assert.Contains("(Body ??= new Thing()).SkuName = value;", processor);
    }

    [Fact]
    public async Task A_Body_Parameter_With_No_Containers_Delegates_To_The_Property_Itself()
    {
        TransformedIR ir = Document();
        // An empty `via` names the body model's own property, so no flattened accessor is involved.
        ir.Operations =
        [
            Operation([
                new FlattenedParameter { Name = "Properties", Property = Id("Thing.properties"), Via = [], Required = true },
            ]),
        ];
        ir.Commands =
        [
            Command([
                Commands.Parameter(
                    "Properties",
                    Commands.FromBody(Id("Thing.properties")),
                    new IRTypeRefModel { Id = Id("ThingProperties") }),
            ]),
        ];

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        Assert.Empty(result.Diagnostics);
        string processor = files.Files.Source(result, "CreateProcessor");
        Assert.Contains("public ThingProperties? Properties { get => Body?.Properties;", processor);
        AssertParses(processor);
    }

    [Fact]
    public async Task An_Unresolvable_Member_Warns_And_Skips_Only_That_Member()
    {
        TransformedIR ir = Document();
        ir.Models[0].Rendering = new ModelHints
        {
            Flattened =
            [
                Member("Size", false, Id("ThingProperties.size"), via: [Id("Thing.properties")]),
                Member("Ghost", true, Id("ThingProperties.missing"), via: [Id("Thing.properties")]),
            ],
        };

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        // A bad hint degrades: the member is dropped, the rest of the model is intact.
        Assert.True(result.Success);
        GeneratorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("ace-generator/unresolved-flattened-path", diagnostic.Code);

        string thing = files.Files.Source(result, "Thing");
        Assert.DoesNotContain("Ghost", thing);
        Assert.Contains("public long Size", thing);
        Assert.Contains("public ThingProperties Properties", thing);
        AssertParses(thing);
    }

    [Fact]
    public async Task A_Model_Without_Flattening_Metadata_Is_Emitted_Exactly_As_Before()
    {
        TransformedIR ir = Document();
        ir.Models[0].Rendering = null;

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);
        string thing = files.Files.Source(result, "Thing");

        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain("[JsonIgnore]", thing);
        Assert.Contains("public ThingProperties Properties", thing);
    }

    [Fact]
    public async Task A_Hoisted_Container_Leaves_The_Public_Surface_But_Still_Serializes()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document(hoisted: true));

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        string thing = files.Files.Source(result, "Thing");

        // Hidden: its members already render as flattened accessors, so a public container
        // would duplicate every one of them.
        Assert.Contains("internal ThingProperties Properties", thing);
        Assert.DoesNotContain("public ThingProperties Properties", thing);

        // But it is still what carries the wire shape, and System.Text.Json skips a
        // non-public member without being told otherwise.
        Assert.Contains("[JsonInclude]", thing);
        Assert.Contains("[JsonPropertyName(\"properties\")]", thing);

        // The accessors over it are untouched, and still compile against an internal member.
        Assert.Contains("public long Size", thing);
        Assert.Contains("get => Properties.Size;", thing);
        AssertParses(thing);
    }

    [Fact]
    public async Task An_Unhoisted_Object_Property_Stays_Public_And_Needs_No_JsonInclude()
    {
        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(Document());
        string thing = files.Files.Source(result, "Thing");

        Assert.Contains("public ThingProperties Properties", thing);
        Assert.DoesNotContain("[JsonInclude]", thing);
    }

    [Fact]
    public async Task A_Read_Only_Flattened_Member_Gets_No_Setter()
    {
        TransformedIR ir = Document();
        ir.Models[0].Rendering = new ModelHints
        {
            Flattened =
            [
                ReadOnly("Size", false, Id("ThingProperties.size"), via: [Id("Thing.properties")]),
                Member("SkuName", true, Id("Sku.name"), via: [Id("Thing.properties"), Id("ThingProperties.sku")]),
            ],
        };

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);
        string thing = files.Files.Source(result, "Thing");

        Assert.Empty(result.Diagnostics);
        // Server-owned: readable, never writable.
        Assert.Contains("public long Size", thing);
        Assert.Contains("get => Properties.Size;", thing);
        Assert.DoesNotContain("Properties.Size = value", thing);

        // Its sibling declares no visibility at all, which means unconstrained — so it keeps a setter.
        Assert.Contains("Properties.Sku.Name = value!;", thing);
        AssertParses(thing);
    }

    [Fact]
    public async Task A_Member_Visible_For_A_Write_Phase_Keeps_Its_Setter()
    {
        TransformedIR ir = Document();
        ir.Models[0].Rendering = new ModelHints
        {
            Flattened = [Writable("Size", false, Id("ThingProperties.size"), via: [Id("Thing.properties")])],
        };

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);
        string thing = files.Files.Source(result, "Thing");

        Assert.Empty(result.Diagnostics);
        Assert.Contains("Properties.Size = value", thing);
        AssertParses(thing);
    }

    [Fact]
    public async Task Body_Stays_Public_When_No_Flat_Member_Resolves()
    {
        TransformedIR ir = Document();
        // Every projected parameter is unresolvable, so the processor gets no flat surface.
        // Hiding Body here would leave the operation with no way to send a payload at all.
        ir.Operations =
        [
            Operation([
                new FlattenedParameter
                {
                    Name = "Ghost",
                    Property = Id("ThingProperties.missing"),
                    Via = [Id("Thing.properties")],
                    Required = false,
                },
            ]),
        ];
        ir.Commands =
        [
            Command([
                Commands.Parameter(
                    "Ghost",
                    Commands.FromBody(Id("ThingProperties.missing"), via: [Id("Thing.properties")]),
                    required: false),
            ]),
        ];

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);
        string processor = files.Files.Source(result, "CreateProcessor");

        GeneratorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ace-generator/unresolved-flattened-parameter", diagnostic.Code);
        Assert.Contains("public Thing? Body", processor);
        AssertParses(processor);
    }
}
