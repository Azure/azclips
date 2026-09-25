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

    private static IRModel Model(string name, params IRProperty[] properties) =>
        new() { Id = Id(name), Name = name, Source = Module, Properties = properties };

    private static FlattenedMember Member(string name, bool optional, params IRId[] path) =>
        new() { Name = name, Path = path, Optional = optional };

    /// <summary>A member no hop exposes for create or update — server-owned.</summary>
    private static FlattenedMember ReadOnly(string name, bool optional, params IRId[] path) =>
        new() { Name = name, Path = path, Optional = optional, Visibility = [FlattenedMemberVisibility.Read] };

    /// <summary>A member whose hops intersect on a write phase, so it stays settable.</summary>
    private static FlattenedMember Writable(string name, bool optional, params IRId[] path) =>
        new()
        {
            Name = name,
            Path = path,
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
                Member("Size", false, Id("Thing.properties"), Id("ThingProperties.size")),
                // Crosses an optional hop, so it widens to nullable.
                Member("SkuName", true, Id("Thing.properties"), Id("ThingProperties.sku"), Id("Sku.name")),
            ],
        };

        IRModel thingProperties = Model(
            "ThingProperties",
            Prim("ThingProperties", "Size", optional: false, int64: true),
            Obj("ThingProperties", "Sku", "Sku"));
        thingProperties.Rendering = new ModelHints
        {
            Flattened = [Member("SkuName", true, Id("ThingProperties.sku"), Id("Sku.name"))],
        };

        var ir = new TransformedIR
        {
            SchemaVersion = "0.13.0",
            Sources = [new IRSource { Id = Module, Server = ServerId() }],
            Servers = [new IRServerDefinition { Id = ServerId(), Endpoint = new IRServerLiteral { Url = "https://example.com" } }],
            Models = [thing, thingProperties, Model("Sku", Prim("Sku", "Name", optional: false))],
            Operations = [],
            Commands = [],
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
                    Commands.FromBody(Id("Thing.properties"), Id("ThingProperties.size")),
                    new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.Int64 }),
                Commands.Parameter(
                    "SkuName",
                    Commands.FromBody(Id("Thing.properties"), Id("ThingProperties.sku"), Id("Sku.name")),
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
                    new FlattenedParameter { Name = "Size", Path = [Id("Thing.properties"), Id("ThingProperties.size")], Required = true },
                    new FlattenedParameter { Name = "SkuName", Path = [Id("Thing.properties"), Id("ThingProperties.sku"), Id("Sku.name")], Required = false },
                ],
            },
        },
    };

    private static async Task<(GenerationResult Result, GeneratedFileCollector Files)> RunAsync(TransformedIR ir)
    {
        var files = new GeneratedFileCollector();
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir, new GeneratorOptions { RootNamespace = "Contoso.Clients" }, files.WriteAsync);
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
    public async Task A_Renamed_Body_Parameter_Still_Resolves_To_Its_Model_Member_By_Path()
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
                    Path = [Id("Thing.properties"), Id("ThingProperties.sku"), Id("Sku.name")],
                    Required = false,
                },
            ]),
        ];
        ir.Commands =
        [
            Command([
                Commands.Parameter(
                    "ThingSkuName",
                    Commands.FromBody(Id("Thing.properties"), Id("ThingProperties.sku"), Id("Sku.name")),
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
    public async Task An_Unresolvable_Path_Warns_And_Skips_Only_That_Member()
    {
        TransformedIR ir = Document();
        ir.Models[0].Rendering = new ModelHints
        {
            Flattened =
            [
                Member("Size", false, Id("Thing.properties"), Id("ThingProperties.size")),
                Member("Ghost", true, Id("Thing.properties"), Id("ThingProperties.missing")),
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
                ReadOnly("Size", false, Id("Thing.properties"), Id("ThingProperties.size")),
                Member("SkuName", true, Id("Thing.properties"), Id("ThingProperties.sku"), Id("Sku.name")),
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
            Flattened = [Writable("Size", false, Id("Thing.properties"), Id("ThingProperties.size"))],
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
                    Path = [Id("Thing.properties"), Id("ThingProperties.missing")],
                    Required = false,
                },
            ]),
        ];
        ir.Commands =
        [
            Command([
                Commands.Parameter(
                    "Ghost",
                    Commands.FromBody(Id("Thing.properties"), Id("ThingProperties.missing")),
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
