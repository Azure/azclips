using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using Ace.Generator.Model.Ir;
using Azure.Bicep.Types;
using Azure.Bicep.Types.Concrete;
using Azure.Bicep.Types.Index;
using Azure.Bicep.Types.Serialization;
using Xunit;

namespace Ace.DeploymentAudit.Tests;

public sealed class CompatibilityAuditTests
{
    private const string Selection = "Service/widgets@2024-01-01";
    private static IRId Id(string name) => new() { Module = "Service", Namespace = ["Service"], Name = name };
    private static IRTypeRefPrimitive Text() => new() { Name = IRTypeRefPrimitiveName.String };
    private static IRPropertyPrimitive Property(string name, bool nullable = false) => new()
    {
        Id = Id($"Widget.{name}"), Name = name, WireName = name, Nullable = nullable, Optional = true, Type = Text(),
    };

    private static TransformedIR Document(IRProperty property) => new()
    {
        SchemaVersion = "0.17.0",
        Sources = [new() { Id = "Service", ApiVersion = new() { Value = "2024-01-01" } }],
        Models = [new() { Id = Id("Widget"), Name = "Widget", Source = "Service", Properties = [property] }],
        Operations = [new() {
            Id = Id("create"), Source = "Service", Method = IROperationMethod.PUT, ApiVersion = new() { Value = "2024-01-01" },
            RequestBody = new() { Type = new IRTypeRefModel { Id = Id("Widget") } },
            PathParameters = [new() { Id = Id("create.name"), Name = "name", WireName = "name", Type = Text() }],
        }],
        Deployment = new() {
            SchemaVersion = "1.0.0",
            Resources = [new() { Operation = Id("create"), ResourceType = "Service/widgets", ApiVersion = "2024-01-01",
                WritableScope = IRDeploymentResourceWritableScope.Tenant, Name = [new IRDeploymentBindingParameter { Parameter = Id("create.name") }] }],
            Provenance = new() { SourceHash = new string('a', 64), Tools = [new() { Name = "fixture", Version = "1" }] },
        },
    };

    [Fact]
    public void Generated_Type_Json_Uses_Official_References_And_The_Same_Audit()
    {
        var ir = Document(Property("value"));
        var catalogue = new Catalogue();
        catalogue.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalogue.Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.None, null),
        });
        AuditReport original = catalogue.Run(ir, Selection);
        var loader = new GeneratedTypeLoader(catalogue.Definitions);
        AuditReport generated = CompatibilityAudit.Run(ir, loader, [Selection], "generated-upstream-types-json",
            Convert.ToHexStringLower(SHA256.HashData(catalogue.Definitions)), new string('a', 64));

        Assert.Equal(original.Results[0].Status, generated.Results[0].Status);
        Assert.Equal(original.Results[0].Coverage.ComparedPaths, generated.Results[0].Coverage.ComparedPaths);
        Assert.Empty(generated.Results[0].Diagnostics);
        Assert.Equal("generated-upstream-types-json", generated.Catalogue);
    }

    [Fact]
    public void Generated_Type_Json_Without_A_Resource_Fails_Explicitly()
    {
        var error = Assert.Throws<AuditInputException>(() => new GeneratedTypeLoader("[]"u8.ToArray()));

        Assert.Equal("generated-resource-missing", error.Code);
    }

    [Fact]
    public async Task Worker_Drains_Large_Input_Before_Returning_Argument_Diagnostics()
    {
        var start = new ProcessStartInfo("dotnet") {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add(typeof(CompatibilityAudit).Assembly.Location);
        start.ArgumentList.Add("--catalogue");
        start.ArgumentList.Add("wrong");
        using var process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(new string(' ', 1024 * 1024));
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        Assert.Equal(1, process.ExitCode);
        using JsonDocument report = JsonDocument.Parse(await output);
        Assert.Equal("catalogue-pin-required", report.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Contains("deployment-audit:", await error);
    }

    [Fact]
    public void Compatible_Envelope_Preserves_Constraints_And_Explicitly_Unverified_Association()
    {
        var ir = Document(Property("secret"));
        ir.Deployment!.Selection = [Selection];
        ir.Deployment!.Facets = [new() { Target = Id("Widget.secret"), TypePath = [], MinLength = 8, MaxLength = 128, Sensitive = true }];
        var catalog = new Catalogue();
        var text = catalog.Ref(new StringType(true, 8, 128, null));
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> { ["secret"] = new(text, ObjectTypePropertyFlags.None, null) });
        AuditReport report = catalog.Run(ir, Selection);
        AuditResult result = Assert.Single(report.Results);
        Assert.Equal("compatible", result.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Contains("$.secret", result.Coverage.ComparedPaths);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("unverified", report.CompilerAssociation);
        Assert.Equal(Selection, Assert.Single(report.ExtractionSelection!));
        Assert.All(report.Tools, tool => Assert.Matches("^[a-f0-9]{64}$", tool.AssemblySha256));
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(report, new JsonSerializerOptions(IrJson.Options) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        Assert.Equal("compatible", json.RootElement.GetProperty("results")[0].GetProperty("status").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal(Selection, json.RootElement.GetProperty("extractionSelection")[0].GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("limitations").EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejects_Empty_Or_Stale_Extraction_Selection(bool stale)
    {
        var ir = Document(Property("value"));
        ir.Deployment!.Selection = stale ? ["Service/other@2024-01-01"] : [];
        var catalog = new Catalogue();
        AuditReport report = catalog.Run(ir, Selection);
        AuditResult result = Assert.Single(report.Results);
        Assert.Equal("unsupported", result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "extraction-selection-invalid");
        Assert.Equal(1, report.ExitCode);
    }

    [Theory]
    [InlineData(false, "enum-narrowing")]
    [InlineData(true, "closed-enum-membership")]
    public void Reports_Enum_Openness_And_Membership_Differences(bool isFixed, string code)
    {
        var property = new IRPropertyEnum { Id = Id("Widget.status"), Name = "status", WireName = "status", Optional = true,
            Type = new() { Id = Id("Status") } };
        var ir = Document(property);
        ir.Enums = [new() { Id = Id("Status"), Name = "Status", Source = "Service", ValueType = IREnumValueType.String,
            IsFixed = isFixed, Members = [new() { Name = "One", Value = "one" }, new() { Name = "Two", Value = "two" }] }];
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["status"] = new(catalog.Ref(new StringLiteralType("one")), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal("incompatible", result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Path == "$.status");
    }

    [Fact]
    public void Missing_Version_Does_Not_Select_Another_Version()
    {
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.None, null) }, "2023-01-01");
        AuditReport report = catalog.Run(Document(Property("value")), Selection);
        Assert.Equal(1, report.ExitCode);
        Assert.Contains(Assert.Single(report.Results).Diagnostics, diagnostic => diagnostic.Code == "catalogue-version-missing");
    }

    [Fact]
    public void Recursive_References_Do_Not_Drop_Later_Property_Differences()
    {
        var ir = Document(new IRPropertyObject { Id = Id("Widget.config"), Name = "config", WireName = "config", Optional = true,
            Type = new IRTypeRefModel { Id = Id("Node") } });
        ir.Models = [ir.Models[0], new() { Id = Id("Node"), Name = "Node", Source = "Service", Properties = [
            new IRPropertyObject { Id = Id("Node.child"), Name = "child", WireName = "child", Optional = true, Nullable = true,
                Type = new IRTypeRefModel { Id = Id("Node") } },
            Property("value"),
        ] }];
        var catalog = new Catalogue();
        var deferred = new Reference();
        var child = catalog.Ref(new UnionType([deferred, catalog.Ref(new NullType())]));
        var node = new ObjectType("Node", new Dictionary<string, ObjectTypeProperty> {
            ["child"] = new(child, ObjectTypePropertyFlags.None, null),
            ["value"] = new(catalog.Ref(new IntegerType(null, null)), ObjectTypePropertyFlags.None, null),
        }, null, null);
        deferred.Type = node;
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> { ["config"] = new(catalog.Ref(node), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal("incompatible", result.Status);
        Assert.Contains("$.config.child", result.Coverage.RecursiveReferences);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Path == "$.config.value" && diagnostic.Code == "primitive-type");
    }

    [Fact]
    public void Mixed_Results_Return_Nonzero_And_Are_Deterministic()
    {
        var ir = Document(Property("value"));
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.None, null) });
        AuditReport report = catalog.Run(ir, Selection, "Service/missing@2024-01-01");
        AuditReport reversed = catalog.Run(ir, "Service/missing@2024-01-01", Selection);
        Assert.Equal(1, report.ExitCode);
        Assert.Contains(report.Results, result => result.Status == "compatible");
        Assert.Contains(report.Results, result => result.Status == "unsupported");
        Assert.Equal(JsonSerializer.Serialize(report, IrJson.Options), JsonSerializer.Serialize(reversed, IrJson.Options));
    }

    [Fact]
    public void Floating_Point_Is_Unsupported_Not_Narrowed_To_Integer()
    {
        var property = Property("value");
        property.Type = new() { Name = IRTypeRefPrimitiveName.Float64 };
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new IntegerType(null, null)), ObjectTypePropertyFlags.None, null) });
        AuditReport report = catalog.Run(Document(property), Selection);
        AuditResult result = Assert.Single(report.Results);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal("unsupported", result.Status);
        Assert.Contains("$.value", result.Coverage.UnsupportedPaths);
        Assert.DoesNotContain("$.value", result.Coverage.ComparedPaths);
    }

    [Fact]
    public void Singleton_Name_Fits_A_Generic_Declaration_Name_Without_An_Input()
    {
        var ir = Document(Property("value"));
        ir.Deployment!.Resources[0].Name = [new IRDeploymentBindingLiteral { Value = "default" }];
        ir.Operations[0].PathParameters = [];
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.None, null) });
        Assert.Equal(0, catalog.Run(ir, Selection).ExitCode);
    }

    [Fact]
    public void Wrong_Type_Catalogue_Reference_Is_Structured_Unsupported()
    {
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.None, null) });
        catalog.CorruptResourceReference();
        AuditReport report = catalog.Run(Document(Property("value")), Selection);
        Assert.Equal(1, report.ExitCode);
        Assert.Contains(Assert.Single(report.Results).Diagnostics, diagnostic => diagnostic.Code == "catalogue-definition-unreadable");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    public void Open_String_Union_Preserves_Enum_Openness_And_Nullability(bool isFixed, bool sourceNullable, bool nativeNullable)
    {
        var property = new IRPropertyEnum { Id = Id("Widget.status"), Name = "status", WireName = "status", Optional = true,
            Nullable = sourceNullable, Type = new() { Id = Id("Status") } };
        var ir = Document(property);
        ir.Enums = [new() { Id = Id("Status"), Name = "Status", Source = "Service", ValueType = IREnumValueType.String,
            IsFixed = isFixed, Members = [new() { Name = "Known", Value = "known" }] }];
        var catalog = new Catalogue();
        List<ITypeReference> variants = [catalog.Ref(new StringLiteralType("known")), catalog.Ref(new StringType(null, null, null, null))];
        if (nativeNullable) variants.Add(catalog.Ref(new NullType()));
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["status"] = new(catalog.Ref(new UnionType(variants)), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal(!isFixed && sourceNullable == nativeNullable ? "compatible" : "incompatible", result.Status);
        Assert.Empty(result.Coverage.UnsupportedPaths);
        if (isFixed) Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "closed-enum-membership");
        if (sourceNullable != nativeNullable) Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "nullability");
        if (!isFixed && sourceNullable == nativeNullable) Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(3, false)]
    [InlineData(null, true)]
    public void Open_String_Union_Preserves_Primitive_Constraints_And_Sensitivity(int? minLength, bool sensitive)
    {
        var property = Property("value");
        var ir = Document(property);
        if (minLength is not null || sensitive)
            ir.Deployment!.Facets = [new() { Target = property.Id, TypePath = [], MinLength = minLength, Sensitive = sensitive }];
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new UnionType([
                catalog.Ref(new StringType(null, null, null, null)), catalog.Ref(new StringLiteralType("known")),
            ])), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal(minLength is not null || sensitive ? "incompatible" : "compatible", result.Status);
        if (minLength is not null || sensitive)
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == (sensitive ? "sensitivity" : "constraint-minLength"));
        else Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Coverage.UnsupportedPaths);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("pattern")]
    [InlineData("sensitive")]
    [InlineData("mixed")]
    public void Nontrivial_String_Unions_Remain_Unsupported(string shape)
    {
        var catalog = new Catalogue();
        TypeBase branch = shape switch {
            "length" => new StringType(null, 3, 10, null),
            "pattern" => new StringType(null, null, null, "^[a-z]+$"),
            "sensitive" => new StringType(true, null, null, null),
            "mixed" => new IntegerType(null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new UnionType([
                catalog.Ref(branch), catalog.Ref(new StringLiteralType("known")),
            ])), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(Document(Property("value")), Selection).Results);
        Assert.Equal("unsupported", result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "catalogue-union-unsupported");
        Assert.DoesNotContain("$.value", result.Coverage.ComparedPaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Writable_Root_Id_Cannot_Be_Silently_Treated_As_Read_Only(bool readOnly)
    {
        var property = Property("id");
        if (readOnly) property.Visibility = [IRVisibility.Read];
        var catalog = new Catalogue();
        catalog.Resource([]);
        AuditResult result = Assert.Single(catalog.Run(Document(property), Selection).Results);
        Assert.Equal(readOnly ? "compatible" : "unsupported", result.Status);
        if (readOnly) Assert.Empty(result.Diagnostics);
        else
        {
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "writable-identity-envelope" && diagnostic.Path == "$.id");
            Assert.DoesNotContain("$.id", result.Coverage.ComparedPaths);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Int32_Requires_Matching_Catalogue_Range_Not_Implicit_Widening(bool bounded)
    {
        var property = Property("value");
        property.Type = new() { Name = IRTypeRefPrimitiveName.Int32 };
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new IntegerType(bounded ? int.MinValue : null, bounded ? int.MaxValue : null)),
                ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(Document(property), Selection).Results);
        Assert.Equal(bounded ? "compatible" : "incompatible", result.Status);
        if (bounded) Assert.Empty(result.Diagnostics);
        else
        {
            AuditDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("integer-range", diagnostic.Code);
            Assert.Contains("[-2147483648, 2147483647]", diagnostic.Message);
            Assert.Contains("[-9223372036854775808, 9223372036854775807]", diagnostic.Message);
        }
    }

    [Fact]
    public void Unsupported_Facet_Type_Cannot_Be_Silently_Ignored()
    {
        var ir = Document(Property("value"));
        ir.Deployment!.Facets = [new() { Target = Id("Widget.value"), TypePath = [], MinItems = 1 }];
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal("unsupported", result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "facet-type-unsupported");
    }

    [Fact]
    public void Nonintegral_Integer_Bounds_Are_Unsupported_At_The_Actual_Wire_Path()
    {
        var property = Property("value");
        property.Type = new() { Name = IRTypeRefPrimitiveName.Int32 };
        var ir = Document(property);
        ir.Deployment!.Facets = [new() { Target = property.Id, TypePath = [], MinValue = "1.5" }];
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new IntegerType(int.MinValue, int.MaxValue)), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal("unsupported", result.Status);
        Assert.Contains("$.value", result.Coverage.UnsupportedPaths);
        Assert.DoesNotContain("$.value", result.Coverage.ComparedPaths);
    }

    [Fact]
    public void Dictionary_Sensitivity_Is_Compared()
    {
        var ir = Document(new IRPropertyObject {
            Id = Id("Widget.value"), Name = "value", WireName = "value", Optional = true,
            Type = new IRTypeRefDictionary { Value = Text() },
        });
        ir.Deployment!.Facets = [new() { Target = Id("Widget.value"), TypePath = [], Sensitive = true }];
        var catalog = new Catalogue();
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> {
            ["value"] = new(catalog.Ref(new ObjectType("Dictionary", new Dictionary<string, ObjectTypeProperty>(),
                catalog.Ref(new StringType(null, null, null, null)), false)), ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal("incompatible", result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "sensitivity" && diagnostic.Path == "$.value");
    }

    [Fact]
    public void Discriminator_Wire_Name_And_Tag_Membership_Are_Compared()
    {
        var ir = Document(new IRPropertyObject { Id = Id("Widget.config"), Name = "config", WireName = "config", Optional = true,
            Type = new IRTypeRefModel { Id = Id("Config") } });
        ir.Models = [ir.Models[0], new() {
            Id = Id("Config"), Name = "Config", Source = "Service",
            Properties = [new IRPropertyPrimitive { Id = Id("Config.kind"), Name = "kind", WireName = "kind", Type = Text() }],
            Polymorphism = new() { Discriminator = Id("Config.kind"), Subtypes = [] },
        }];
        var catalog = new Catalogue();
        var variant = catalog.Ref(new ObjectType("Other", new Dictionary<string, ObjectTypeProperty>(), null, null));
        var discriminated = catalog.Ref(new DiscriminatedObjectType("Config", "type",
            new Dictionary<string, ObjectTypeProperty>(), new Dictionary<string, ITypeReference> { ["other"] = variant }));
        catalog.Resource(new Dictionary<string, ObjectTypeProperty> { ["config"] = new(discriminated, ObjectTypePropertyFlags.None, null) });
        AuditResult result = Assert.Single(catalog.Run(ir, Selection).Results);
        Assert.Equal("incompatible", result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "discriminator-wire-name");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "discriminator-tags");
    }

    private sealed class Reference : ITypeReference
    {
        public TypeBase Type { get; set; } = default!;
    }

    private sealed class Catalogue : TypeLoader
    {
        private readonly List<TypeBase> types = [];
        private readonly Dictionary<string, CrossFileTypeReference> resources = new(StringComparer.Ordinal);
        private byte[]? definitions;
        private byte[]? index;
        public byte[] Definitions => definitions ?? throw new InvalidOperationException("Fixture types were not serialized.");

        public ITypeReference Ref(TypeBase type)
        {
            if (!types.Contains(type)) types.Add(type);
            return new Reference { Type = type };
        }

        public void Resource(Dictionary<string, ObjectTypeProperty> properties, string version = "2024-01-01")
        {
            properties["name"] = new(Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.Required, null);
            properties["id"] = new(Ref(new StringType(null, null, null, null)), ObjectTypePropertyFlags.ReadOnly, null);
            var body = Ref(new ObjectType("Widget", properties, null, null));
            var resource = new ResourceType($"Service/widgets@{version}", body, null, ScopeType.Tenant, ScopeType.Tenant, null, null, null);
            Ref(resource);
            resources[resource.Name] = new("types.json", types.IndexOf(resource));
        }

        public void CorruptResourceReference() => resources[Selection] = new("types.json", 0);

        public AuditReport Run(TransformedIR ir, params string[] selections)
        {
            using var definitionsStream = new MemoryStream();
            TypeSerializer.Serialize(definitionsStream, types.ToArray());
            definitions = definitionsStream.ToArray();
            using var indexStream = new MemoryStream();
            TypeSerializer.SerializeIndex(indexStream, new TypeIndex(resources,
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<CrossFileTypeReference>>>(), [], null, null));
            index = indexStream.ToArray();
            return CompatibilityAudit.Run(ir, this, selections, "fixture-catalogue",
                Convert.ToHexStringLower(SHA256.HashData(definitions.Concat(index).ToArray())),
                Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ir, IrJson.Options))));
        }

        protected override Stream GetContentStreamAtPath(string path) => new MemoryStream(path switch {
            "index.json" => index ?? throw new InvalidOperationException("Fixture index was not serialized."),
            "types.json" => definitions ?? throw new InvalidOperationException("Fixture types were not serialized."),
            _ => throw new FileNotFoundException($"Unknown fixture catalogue file '{path}'."),
        }, writable: false);
    }
}
