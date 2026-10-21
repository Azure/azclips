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
        Id = OperationId(name),
        Name = name,
        Source = "First",
        Server = server,
        PathTemplate = "/widgets",
    };

    private static IRId OperationId(string name) => new() { Module = "First", Name = name };

    /// <summary>
    /// One operation and the command projected from it. The generator reads commands, so the
    /// server reference under test lives on the command; the operation is carried alongside as
    /// the wire truth the command points back at.
    /// </summary>
    private static TransformedIR Document(IRId? reference = null) => new()
    {
        SchemaVersion = "0.13.0",
        Sources = [new IRSource { Id = "First", Server = ServerId() }],
        Servers = [Definition(ServerId())],
        Operations = [Operation("get", ServerId())],
        Commands = [Commands.Atomic("Get", reference ?? ServerId(), OperationId("get"))],
        Target = Targets.For("Contoso.Clients"),
    };

    private static async Task<(GenerationResult Result, GeneratedFileCollector Files)> RunAsync(TransformedIR ir)
    {
        var files = new GeneratedFileCollector();
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir, config: null, files.WriteAsync);
        return (result, files);
    }

    private static void AssertUri(
        GenerationResult result, GeneratedFileCollector files, string className, string uri, string appended = "")
    {
        Assert.Contains($"CreateRequestUri() => $\"{uri}\"{appended};", files.Files.Source(result, className));
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
        Assert.Contains("Get", diagnostic.Message);
        Assert.DoesNotContain("GetProcessor", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Commands_Resolve_Distinct_And_Shared_Servers_Across_Sources()
    {
        TransformedIR ir = Document();
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
        ir.Operations = [Operation("getFirst", ServerId()), second, Operation("listFirst", ServerId()), Operation("crossSource", ServerId("Second"))];
        ir.Commands =
        [
            Commands.Atomic("GetFirst", ServerId(), OperationId("getFirst")),
            Commands.Atomic("GetSecond", ServerId("Second"), OperationId("getSecond")),
            Commands.Atomic("ListFirst", ServerId(), OperationId("listFirst")),
            // The command's explicit reference wins even when its source points elsewhere.
            Commands.Atomic("CrossSource", ServerId("Second"), OperationId("crossSource")),
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
        TransformedIR ir = Document();
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

        // The command carries the resolved request: segments, not a template, and an
        // api-version pinned as a literal rather than surfaced as an input.
        ir.Commands =
        [
            Commands.Atomic(
                "Get",
                ServerId(),
                OperationId("get"),
                path:
                [
                    Commands.Literal("/accounts/"),
                    Commands.Param("AccountName"),
                    Commands.Literal("/widgets/"),
                    Commands.Param("WidgetName"),
                ],
                query:
                [
                    Commands.Pinned("api-version", "2026-01-01"),
                    Commands.Supplied("$top", "PageSize"),
                ],
                parameters:
                [
                    Commands.Parameter("AccountName", Commands.FromPath(OperationId("p.account"))),
                    Commands.Parameter("WidgetName", Commands.FromPath(OperationId("p.widget"))),
                    Commands.Parameter(
                        "PageSize",
                        Commands.FromQuery(OperationId("p.top")),
                        new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.Int32 },
                        required: false),
                ]),
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
            "{Endpoint}/accounts/{Uri.EscapeDataString(AccountName)}/widgets/{Uri.EscapeDataString(WidgetName)}?api-version=2026-01-01",
            ".WithQuery(\"$top\", PageSize)");
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
        TransformedIR ir = Document(Copy(id));
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
        TransformedIR ir = Document(reference);
        ir.Servers = [Definition(id)];

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "unknown-server-reference");
    }

    [Fact]
    public async Task Missing_Reference_Does_Not_Fall_Back_To_Source_Server()
    {
        TransformedIR ir = Document();
        // The generator reads the *command's* reference; an operation's is wire truth it never
        // consults, which is the point — a command does not inherit its source's server.
        ir.Commands[0].Server = null!;

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
        TransformedIR ir = Document();
        ir.Servers = [];

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "unknown-server-reference");
    }

    [Theory]
    [InlineData("https://first.example.com")]
    [InlineData("https://other.example.com")]
    public async Task Duplicate_Definitions_Are_Ambiguous_Even_When_Endpoints_Match(string duplicateUrl)
    {
        TransformedIR ir = Document();
        ir.Servers = [Definition(ServerId()), Definition(ServerId(), duplicateUrl)];

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "ambiguous-server-reference");
    }

    [Fact]
    public async Task Missing_Endpoint_Rejects_Operation()
    {
        TransformedIR ir = Document();
        ir.Servers[0].Endpoint = null!;

        (GenerationResult result, _) = await RunAsync(ir);

        AssertInvalid(result, "missing-server-endpoint");
    }
}
