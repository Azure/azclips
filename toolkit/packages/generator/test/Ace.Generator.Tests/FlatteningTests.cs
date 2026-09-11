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

    private static IRProperty Obj(string owner, string name, string target, bool optional = true) =>
        new IRPropertyObject
        {
            Id = Id($"{owner}.{char.ToLowerInvariant(name[0])}{name[1..]}"),
            Name = name,
            WireName = char.ToLowerInvariant(name[0]) + name[1..],
            Optional = optional,
            Type = new IRTypeRefModel { Id = Id(target) },
        };

    private static IRModel Model(string name, params IRProperty[] properties) =>
        new() { Id = Id(name), Name = name, Source = Module, Properties = properties };

    private static FlattenedMember Member(string name, bool optional, params IRId[] path) =>
        new() { Name = name, Path = path, Optional = optional };

    private static IRId ServerId() => Id("server");

    /// <summary>Thing { properties -&gt; ThingProperties { size, sku -&gt; Sku { name } } }.</summary>
    private static IRDocument Document(bool withOperation = false)
    {
        IRModel thing = Model("Thing", Obj("Thing", "Properties", "ThingProperties", optional: false));
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

        var ir = new IRDocument
        {
            SchemaVersion = "0.12.0",
            Sources = [new IRSource { Id = Module, Server = ServerId() }],
            Servers = [new IRServerDefinition { Id = ServerId(), Endpoint = new IRServerLiteral { Url = "https://example.com" } }],
            Models = [thing, thingProperties, Model("Sku", Prim("Sku", "Name", optional: false))],
            Operations = [],
        };

        if (withOperation)
        {
            ir.Operations = [Operation()];
        }

        return ir;
    }

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

    private static async Task<(GenerationResult Result, GeneratedFileCollector Files)> RunAsync(IRDocument ir)
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

        // Body survives: the flat members are views over it, and CreateRequestBody still returns it.
        Assert.Contains("public Thing? Body", processor);
        Assert.Contains("protected override Thing? CreateRequestBody() => Body;", processor);

        // Each flat member delegates to the model's own member rather than re-walking the path.
        Assert.Contains("public long? Size { get => Body?.Size; set => (Body ??= new Thing()).Size = value ?? default; }", processor);
        Assert.Contains("public string? SkuName { get => Body?.SkuName; set => (Body ??= new Thing()).SkuName = value; }", processor);
        AssertParses(processor);
    }

    [Fact]
    public async Task A_Renamed_Body_Parameter_Still_Resolves_To_Its_Model_Member_By_Path()
    {
        IRDocument ir = Document();
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

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        Assert.Empty(result.Diagnostics);
        string processor = files.Files.Source(result, "CreateProcessor");
        Assert.Contains("public string? ThingSkuName { get => Body?.SkuName;", processor);
        Assert.Contains("(Body ??= new Thing()).SkuName = value;", processor);
    }

    [Fact]
    public async Task An_Unresolvable_Path_Warns_And_Skips_Only_That_Member()
    {
        IRDocument ir = Document();
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
        IRDocument ir = Document();
        ir.Models[0].Rendering = null;

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);
        string thing = files.Files.Source(result, "Thing");

        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain("[JsonIgnore]", thing);
        Assert.Contains("public ThingProperties Properties", thing);
    }
}
