using System.Text.Json;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class ServerReferenceTests
{
    private static IRId ServerId(string module = "First") => new()
    {
        Module = module,
        Namespace = ["Contoso", "Inventory"],
        Name = "server",
    };

    private static IRId Copy(IRId id) =>
        JsonSerializer.Deserialize<IRId>(JsonSerializer.Serialize(id, IrJson.Options), IrJson.Options)!;

    private static IRServerDefinition Definition(IRId id, string url = "https://first.example.com") => new()
    {
        Id = id,
        Endpoint = new IRServerLiteral { Url = url },
    };

    private static IROperation Operation(string name, IRId server) => new()
    {
        Id = new IRId { Module = "First", Name = name },
        Name = name,
        Source = "First",
        Server = server,
        PathTemplate = "/widgets",
    };

    private static IRDocument Document(IRId? reference = null) => new()
    {
        SchemaVersion = "0.8.0",
        Sources = [new IRSource { Id = "First", Server = ServerId() }],
        Servers = [Definition(ServerId())],
        Operations = [Operation("get", reference ?? ServerId())],
    };

    private static async Task<(GenerationResult Result, GeneratedFileCollector Files)> RunAsync(IRDocument ir)
    {
        var files = new GeneratedFileCollector();
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir, new GeneratorOptions { RootNamespace = "Contoso.Clients" }, files.WriteAsync);
        return (result, files);
    }

    private static void AssertUri(
        GenerationResult result, GeneratedFileCollector files, string className, string uri)
    {
        Assert.Contains($"CreateRequestUri() => $\"{uri}\";", files.Files.Source(result, className));
    }

    /// <summary>
    /// Assert a processor's endpoint seam: <c>null</c> means it matches the base's default and so
    /// says nothing, otherwise it must override with exactly this endpoint.
    /// </summary>
    private static void AssertEndpoint(
        GenerationResult result, GeneratedFileCollector files, string className, string? endpoint)
    {
        string source = files.Files.Source(result, className);
        if (endpoint is null)
        {
            Assert.DoesNotContain("override string Endpoint", source);
        }
        else
        {
            Assert.Contains($"override string Endpoint => \"{endpoint}\";", source);
        }
    }

    private static void AssertInvalid(GenerationResult result, string code)
    {
        Assert.False(result.Success);
        GeneratorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("ace-generator/" + code, diagnostic.Code);
        Assert.Contains("get", diagnostic.Message);
        Assert.DoesNotContain("GetProcessor", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Operations_Resolve_Distinct_And_Shared_Servers_Across_Sources()
    {
        IRDocument ir = Document();
        ir.Sources =
        [
            new IRSource { Id = "First", Server = ServerId() },
            new IRSource { Id = "Second", Server = ServerId("Second") },
        ];
        ir.Servers =
        [
            Definition(ServerId("Second"), "https://second.example.com"),
            Definition(ServerId()),
        ];
        IROperation second = Operation("getSecond", ServerId("Second"));
        second.Source = "Second";
        ir.Operations =
        [
            Operation("getFirst", ServerId()),
            second,
            Operation("listFirst", ServerId()),
            // The operation's explicit reference wins even when its source points elsewhere.
            Operation("crossSource", ServerId("Second")),
        ];

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        // The endpoint is no longer baked into each URI — every processor interpolates the base's
        // seam, and only one whose server disagrees with the base default overrides it.
        AssertUri(result, files, "GetFirstProcessor", "{Endpoint}/widgets");
        AssertUri(result, files, "GetSecondProcessor", "{Endpoint}/widgets");
        AssertUri(result, files, "ListFirstProcessor", "{Endpoint}/widgets");
        AssertUri(result, files, "CrossSourceProcessor", "{Endpoint}/widgets");

        Assert.Contains(
            "virtual string Endpoint => \"https://first.example.com\";",
            files.Files.Source(result, "ProcessorBase"));
        AssertEndpoint(result, files, "GetFirstProcessor", null);
        AssertEndpoint(result, files, "ListFirstProcessor", null);
        AssertEndpoint(result, files, "GetSecondProcessor", "https://second.example.com");
        AssertEndpoint(result, files, "CrossSourceProcessor", "https://second.example.com");
        Assert.Equal(2, ir.Servers.Count);
    }

    [Fact]
    public async Task Parameterized_Server_Preserves_Defaults_And_Parameter_Interpolation()
    {
        IRDocument ir = Document();
        ir.Servers[0].Endpoint = new IRServerParameterized
        {
            Template = "https://{account}.{domain}/{basePath}",
            Parameters =
            [
                new IRServerParameter { Name = "accountName", WireName = "account" },
                new IRServerParameter
                {
                    Name = "domain",
                    WireName = "domain",
                    DefaultValue = new IRLiteralValueString { Value = "example.com" },
                },
                new IRServerParameter
                {
                    Name = "basePath",
                    WireName = "basePath",
                    DefaultValue = new IRLiteralValueString { Value = "api" },
                },
            ],
        };
        IROperation operation = ir.Operations[0];
        operation.PathTemplate = "/accounts/{account}/widgets/{widget}";
        operation.PathParameters =
        [
            new IRParameter
            {
                Name = "accountName",
                WireName = "account",
                Type = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String },
            },
            new IRParameter
            {
                Name = "widgetName",
                WireName = "widget",
                Type = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String },
            },
        ];
        operation.ApiVersion = new IRApiVersionStamp
        {
            Location = IRApiVersionStampLocation.Query,
            ParameterName = "api-version",
            Value = "2026-01-01",
        };
        operation.QueryParameters =
        [
            new IRParameter
            {
                Name = "pageSize",
                WireName = "$top",
                Type = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.Int32 },
            },
        ];

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        Assert.True(result.Success);
        // Defaults are spec truth and get substituted; `account` has none, so it stays a hole the
        // base fills from a public input rather than a value the generator invents.
        string @base = files.Files.Source(result, "ProcessorBase");
        Assert.Contains("protected virtual string Endpoint => $\"https://{AccountName}.example.com/api\";", @base);
        Assert.Contains("public string AccountName { get; set; } = string.Empty;", @base);

        // The URI interpolates the seam, and the processor does not redeclare the inherited input.
        AssertUri(result, files, "GetProcessor",
            "{Endpoint}/accounts/{AccountName}/widgets/{WidgetName}?api-version=2026-01-01&$top={PageSize}");
        string processor = files.Files.Source(result, "GetProcessor");
        Assert.DoesNotContain("public string AccountName", processor);
        Assert.Contains("public string WidgetName", processor);
    }

    [Fact]
    public async Task Server_Identity_Uses_Structural_Equality_For_Recursive_Type_Arguments()
    {
        IRId id = ServerId();
        id.Interface = "Widgets";
        id.TypeArgs = [new IRId { Module = "Models", Name = "Page", TypeArgs = [ServerId("Nested")] }];
        IRDocument ir = Document(Copy(id));
        ir.Servers = [Definition(id)];

        (GenerationResult result, GeneratedFileCollector files) = await RunAsync(ir);

        Assert.True(result.Success);
        AssertUri(result, files, "GetProcessor", "{Endpoint}/widgets");
    }

    [Theory]
    [InlineData("module")]
    [InlineData("namespace")]
    [InlineData("namespaceSegments")]
    [InlineData("interface")]
    [InlineData("name")]
    [InlineData("typeArgModule")]
    [InlineData("typeArgNamespace")]
    [InlineData("typeArgInterface")]
    [InlineData("typeArgName")]
    [InlineData("nestedTypeArg")]
    [InlineData("typeArgCount")]
    [InlineData("typeArgOrder")]
    [InlineData("absentTypeArgs")]
    [InlineData("emptyTypeArgs")]
    public async Task Different_Identity_Components_Do_Not_Resolve(string component)
    {
        IRId id = ServerId();
        id.TypeArgs = [ServerId("ArgOne"), ServerId("ArgTwo")];
        IRId reference = Copy(id);
        switch (component)
        {
            case "module": reference.Module = "first"; break;
            case "namespace": reference.Namespace = ["Contoso", "inventory"]; break;
            case "namespaceSegments": reference.Namespace = ["Contoso.Inventory"]; break;
            case "interface": reference.Interface = "Widgets"; break;
            case "name": reference.Name = "Server"; break;
            case "typeArgModule": reference.TypeArgs![0].Module = "Other"; break;
            case "typeArgNamespace": reference.TypeArgs![0].Namespace = ["Other"]; break;
            case "typeArgInterface": reference.TypeArgs![0].Interface = "Widgets"; break;
            case "typeArgName": reference.TypeArgs![0].Name = "Other"; break;
            case "nestedTypeArg": reference.TypeArgs![0].TypeArgs = [ServerId()]; break;
            case "typeArgCount": reference.TypeArgs = [ServerId("ArgOne")]; break;
            case "typeArgOrder": reference.TypeArgs = [ServerId("ArgTwo"), ServerId("ArgOne")]; break;
            case "absentTypeArgs": reference.TypeArgs = null; break;
            case "emptyTypeArgs": id.TypeArgs = null; reference.TypeArgs = []; break;
        }
        IRDocument ir = Document(reference);
        ir.Servers = [Definition(id)];

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "unknown-server-reference");
    }

    [Fact]
    public async Task Missing_Reference_Does_Not_Fall_Back_To_Source_Server()
    {
        IRDocument ir = Document();
        ir.Operations[0].Server = null!;

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "missing-server-reference");
    }

    [Fact]
    public async Task Unknown_Reference_Does_Not_Fall_Back_To_Source_Server()
    {
        (GenerationResult result, _) = await RunAsync(Document(ServerId("Missing")));

        AssertInvalid(result, "unknown-server-reference");
    }

    [Fact]
    public async Task Missing_Server_Definitions_Rejects_Operation()
    {
        IRDocument ir = Document();
        ir.Servers = [];

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "unknown-server-reference");
    }

    [Theory]
    [InlineData("https://first.example.com")]
    [InlineData("https://other.example.com")]
    public async Task Duplicate_Definitions_Are_Ambiguous_Even_When_Endpoints_Match(string duplicateUrl)
    {
        IRDocument ir = Document();
        ir.Servers = [Definition(ServerId()), Definition(ServerId(), duplicateUrl)];

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "ambiguous-server-reference");
    }

    [Fact]
    public async Task Missing_Endpoint_Rejects_Operation()
    {
        IRDocument ir = Document();
        ir.Servers[0].Endpoint = null!;

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "missing-server-endpoint");
    }
}
