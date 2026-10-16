using System.Runtime.CompilerServices;
using System.Text.Json;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

public sealed class CSharpClientGeneratorTests
{
    private static IRId ServerId() => new() { Module = "Test", Name = "server" };

    private static IRId OperationId(string name) => new() { Module = "Test", Name = name };

    private static IRId EnumId(string name) => new() { Module = "Test", Name = name };

    /// <summary>An enum the document declares, with one member per value.</summary>
    private static IREnum DeclaredEnum(string name, IREnumValueType valueType, params object[] values) => new()
    {
        Id = EnumId(name),
        Name = name,
        ValueType = valueType,
        IsFixed = true,
        Source = "Test",
        Members = [.. values.Select(value => new IREnumMember { Name = $"{value}", Value = value })],
    };

    /// <summary>A <c>Widget</c> property typed by the enum named <paramref name="enumName"/>.</summary>
    private static IRProperty EnumProperty(string name, string enumName, bool optional = false) => new IRPropertyEnum
    {
        Id = new IRId { Module = "Test", Name = $"Widget.{name}" },
        Name = name,
        WireName = char.ToLowerInvariant(name[0]) + name[1..],
        Optional = optional,
        Type = new IRTypeRefEnum { Id = EnumId(enumName) },
    };

    private static string Pascal(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>
    /// A document with one command per named operation. The generator reads commands; the
    /// operations are carried alongside as the wire truth each command points back at.
    /// </summary>
    private static TransformedIR Ir(IEnumerable<string>? models = null, IEnumerable<string>? operations = null)
    {
        string[] names = [.. operations ?? []];
        return new TransformedIR
        {
            SchemaVersion = "0.12.0",
            Servers =
            [
                new IRServerDefinition
                {
                    Id = ServerId(),
                    Endpoint = new IRServerLiteral { Url = "https://example.com" },
                },
            ],
            Models = (models ?? [])
                .Select(name => new IRModel
                {
                    Name = name,
                    Source = "Test",
                    Id = new IRId { Module = "Test", Name = name },
                })
                .ToList(),
            Operations = names
                .Select(name => new IROperation { Id = OperationId(name), Name = name, Server = ServerId() })
                .ToList(),
            Commands = names
                .Select(name => Commands.Atomic(Pascal(name), ServerId(), OperationId(name)))
                .ToList(),
            Target = Targets.For("Contoso.Clients"),
        };
    }

    /// <summary>
    /// Run the generator, writing every emitted file to this test's own
    /// <c>artifacts/&lt;test&gt;/</c> folder (for eyeballing) and mirroring them in
    /// memory (for assertions). The scenario defaults to the calling test's name.
    /// </summary>
    private static async Task<(GenerationResult Result, ArtifactWriter Writer)> RunAsync(
        TransformedIR ir,
        JsonElement? config = null,
        [CallerMemberName] string scenario = "default")
    {
        var writer = new ArtifactWriter(scenario);
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            config,
            writer.WriteAsync);
        return (result, writer);
    }

    [Fact]
    public async Task Generate_EmitsATypePerModel()
    {
        (GenerationResult result, _) = await RunAsync(Ir(models: ["Widget", "Gadget"]));

        Assert.True(result.Success);
        // Contract: one generated type per model, named after the model.
        Assert.Contains("Widget", result.GeneratedClasses.Keys);
        Assert.Contains("Gadget", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Generate_EmitsAProcessorTypePerOperation()
    {
        // Operation names are camelCase in the IR; processor types are PascalCased + "Processor".
        (GenerationResult result, _) = await RunAsync(Ir(operations: ["restoreVolume"]));

        Assert.True(result.Success);
        Assert.Contains("RestoreVolumeProcessor", result.GeneratedClasses.Keys);
    }

    [Fact]
    public async Task Generate_FormatsNonStringHeaderValues()
    {
        // TryAddWithoutValidation only takes string?/IEnumerable<string?>, so a number-typed
        // header has to be formatted. Passing it raw compiles only by accident of overload
        // resolution — and here, not at all. An enum renders as its value type, so only a
        // numeric one is formatted.
        TransformedIR ir = Ir(operations: ["get"]);
        ir.Enums =
        [
            DeclaredEnum("ForceDelete", IREnumValueType.String, "true", "false"),
            DeclaredEnum("Priority", IREnumValueType.Int32, 1, 2),
        ];
        ir.Commands =
        [
            Commands.Atomic(
                "Get",
                ServerId(),
                OperationId("get"),
                headers:
                [
                    Commands.Supplied("x-ms-force-delete", "XMsForceDelete"),
                    Commands.Supplied("x-ms-priority", "Priority"),
                    Commands.Supplied("x-ms-client-request-id", "ClientRequestId"),
                    Commands.Supplied("x-ms-tags", "Tags"),
                ],
                parameters:
                [
                    Commands.Parameter(
                        "XMsForceDelete",
                        Commands.FromHeader(new IRId { Module = "Test", Name = "force" }),
                        new IRTypeRefEnum { Id = EnumId("ForceDelete") }),
                    Commands.Parameter(
                        "Priority",
                        Commands.FromHeader(new IRId { Module = "Test", Name = "priority" }),
                        new IRTypeRefEnum { Id = EnumId("Priority") }),
                    Commands.Parameter(
                        "ClientRequestId",
                        Commands.FromHeader(new IRId { Module = "Test", Name = "reqid" })),
                    Commands.Parameter(
                        "Tags",
                        Commands.FromHeader(new IRId { Module = "Test", Name = "tags" }),
                        new IRTypeRefArray { Element = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String } }),
                ]),
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        string source = writer.Files.Source(result, "GetProcessor");
        // A non-string value is interpolated...
        Assert.Contains("TryAddWithoutValidation(\"x-ms-priority\", $\"{Priority}\")", source);
        // ...while a string, a string enum, and a string collection go straight to their overloads.
        Assert.Contains("TryAddWithoutValidation(\"x-ms-force-delete\", XMsForceDelete)", source);
        Assert.Contains("TryAddWithoutValidation(\"x-ms-client-request-id\", ClientRequestId)", source);
        Assert.Contains("TryAddWithoutValidation(\"x-ms-tags\", Tags)", source);
    }

    [Fact]
    public async Task Generate_RendersEnumsAsTheirValueType()
    {
        // An enum is not a C# type: models and processors alike render it as the primitive its
        // values have. An enum the document does not declare has no known value type.
        TransformedIR ir = Ir(operations: ["list"]);
        ir.Enums =
        [
            DeclaredEnum("Tier", IREnumValueType.String, "Hot", "Cool"),
            DeclaredEnum("Priority", IREnumValueType.Int64, 1L, 2L),
        ];
        ir.Models =
        [
            new IRModel
            {
                Id = new IRId { Module = "Test", Name = "Widget" },
                Name = "Widget",
                Source = "Test",
                Properties =
                [
                    EnumProperty("Tier", "Tier"),
                    EnumProperty("Priority", "Priority", optional: true),
                    EnumProperty("Shade", "Shade"),
                ],
            },
        ];
        ir.Commands =
        [
            Commands.Atomic(
                "List",
                ServerId(),
                OperationId("list"),
                query: [Commands.Supplied("tier", "Tier"), Commands.Supplied("priority", "Priority")],
                parameters:
                [
                    Commands.Parameter(
                        "Tier",
                        Commands.FromQuery(new IRId { Module = "Test", Name = "tier" }),
                        new IRTypeRefEnum { Id = EnumId("Tier") }),
                    Commands.Parameter(
                        "Priority",
                        Commands.FromQuery(new IRId { Module = "Test", Name = "priority" }),
                        new IRTypeRefEnum { Id = EnumId("Priority") },
                        required: false),
                ]),
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        string model = writer.Files.Source(result, "Widget");
        Assert.Contains("public string Tier { get; set; }", model);
        Assert.Contains("public long? Priority { get; set; }", model);
        Assert.Contains("public object Shade { get; set; }", model);
        string processor = writer.Files.Source(result, "ListProcessor");
        // A required string enum gets the same non-null default as any other string parameter.
        Assert.Contains("public string Tier { get; set; } = string.Empty;", processor);
        Assert.Contains("public long? Priority { get; set; }", processor);
    }

    [Fact]
    public async Task Generate_Paging_ReachesANestedItemsPropertyThroughItsContainers()
    {
        // `via` lists the containers outermost first: the selector walks them, then the property.
        IRId envelopeId = new() { Module = "Test", Name = "Envelope" };
        IRId pageId = new() { Module = "Test", Name = "Page" };
        IRId resultId = new() { Module = "Test", Name = "Envelope.result" };
        IRId valueId = new() { Module = "Test", Name = "Page.value" };
        TransformedIR ir = Ir(operations: ["list"]);
        ir.Models =
        [
            new IRModel
            {
                Name = "Envelope",
                Source = "Test",
                Id = envelopeId,
                Properties =
                [
                    new IRPropertyObject
                    {
                        Id = resultId,
                        Name = "Result",
                        WireName = "result",
                        Type = new IRTypeRefModel { Id = pageId },
                    },
                ],
            },
            new IRModel
            {
                Name = "Page",
                Source = "Test",
                Id = pageId,
                Properties =
                [
                    new IRPropertyCollection
                    {
                        Id = valueId,
                        Name = "Value",
                        WireName = "value",
                        Type = new IRTypeRefArray { Element = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String } },
                    },
                ],
            },
        ];
        ir.Commands =
        [
            Commands.Atomic(
                "List",
                ServerId(),
                OperationId("list"),
                pagination: new IRCommandPagination
                {
                    ItemType = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String },
                    Items = new IRPropertyRef { Property = valueId, Via = [resultId] },
                },
                surface: Commands.Surface(response: new IRTypeRefModel { Id = envelopeId }, paginated: true)),
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.Empty(result.Diagnostics);
        string source = writer.Files.Source(result, "ListProcessor");
        Assert.Contains("=> body.Result.Value;", source);
        Assert.Contains("WriteOutputWithPaginationAsync", source);
    }

    [Fact]
    public async Task Generate_Paging_WithAnUnresolvableReference_DiagnosesRatherThanInventingAMember()
    {
        // A property id that names no property in the document must not be PascalCased into a
        // plausible-looking member: that compiles into a wrong selector. Report and emit nothing.
        TransformedIR ir = Ir(operations: ["list"]);
        ir.Commands =
        [
            Commands.Atomic(
                "List",
                ServerId(),
                OperationId("list"),
                pagination: new IRCommandPagination
                {
                    ItemType = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String },
                    Items = new IRPropertyRef { Property = new IRId { Module = "Test", Name = "ghost" } },
                },
                surface: Commands.Surface(paginated: true)),
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d =>
            d.Severity == DiagnosticSeverity.Warning &&
            d.Code == "ace-generator/paging-path-unresolved");

        string source = writer.Files.Source(result, "ListProcessor");
        Assert.DoesNotContain("Ghost", source);
        Assert.DoesNotContain("WriteOutputWithPaginationAsync", source);
    }

    [Fact]
    public async Task Generate_MultipartTuple_EmitsScaffoldAndReportsSerializationGap()
    {
        TransformedIR ir = Ir(operations: ["upload"]);
        ir.Commands[0].Steps[0].Request!.Body = new IRCommandRequestBody
        {
            ContentType = "multipart/mixed",
            Multipart = new IRMultipartBody
            {
                Kind = IRMultipartBodyKind.Tuple,
                ContentTypes = ["multipart/mixed"],
                Parts =
                [
                    new IRMultipartPart
                    {
                        Body = new IRMultipartPartBodySingle
                        {
                            Type = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.Bytes },
                            ContentTypes = ["application/octet-stream"],
                        },
                    },
                ],
            },
        };

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Warning &&
            diagnostic.Code == "ace-generator/multipart-serialization-not-supported");
        Assert.Contains("object? Body", writer.Files.Source(result, "UploadProcessor"));
    }

    [Fact]
    public async Task Generate_SpellsTheCommandsNameVerbatim()
    {
        // Naming is the projection's job: it normalizes and resolves collisions once, across the
        // whole document. Re-deriving a name here would silently undo that, so the generator must
        // spell exactly what the command carries — neither the operation's name nor its id.
        TransformedIR ir = Ir();
        ir.Operations =
        [
            new IROperation
            {
                Id = new IRId { Module = "Test", Namespace = ["Contoso", "Inventory"], Interface = "Widgets", Name = "read" },
                Name = "read",
                Server = ServerId(),
            },
        ];
        ir.Commands =
        [
            Commands.Atomic(
                "FetchWidgets",
                ServerId(),
                new IRId { Module = "Test", Namespace = ["Contoso", "Inventory"], Interface = "Widgets", Name = "read" }),
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Contains("class FetchWidgetsProcessor", writer.Files.Source(result, "FetchWidgetsProcessor"));
        Assert.DoesNotContain(
            result.GeneratedClasses.Keys,
            k => k is "ReadProcessor" or "ReadWidgetsProcessor" or "ReadInventoryProcessor");
    }

    [Fact]
    public async Task Generate_EmitsAProcessorPerCommand_IncludingSiblingsOverOneOperation()
    {
        // Commands, not operations, are the unit: variants project several commands from a single
        // operation, and each is its own processor.
        IRId operation = OperationId("get");
        TransformedIR ir = Ir();
        ir.Operations = [new IROperation { Id = operation, Name = "get", Server = ServerId() }];
        ir.Commands =
        [
            Commands.Atomic("GetWidget", ServerId(), operation, action: "get"),
            Commands.Atomic("GetWidgetByName", ServerId(), operation, action: "getByName"),
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            new[] { "GetWidgetByNameProcessor", "GetWidgetProcessor", "ProcessorBase" },
            result.GeneratedClasses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(3, writer.Files.ProcessorSources().Count());
    }

    [Fact]
    public async Task Generate_AlwaysEmitsProcessorBaseAndProjectFile()
    {
        (GenerationResult result, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget"]));

        Assert.Contains("ProcessorBase", result.GeneratedClasses.Keys);
        // A .csproj is a platform requirement, not a volatile naming decision.
        Assert.Contains(writer.Files.Keys, k => k.EndsWith(".csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_MapsEachClassToAWrittenFile()
    {
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["restoreVolume"]));

        // Every mapped class points at a file that was actually written...
        foreach (string path in result.GeneratedClasses.Values)
        {
            Assert.Contains(path, writer.Files.Keys);
        }
        // ...the project file is not a class...
        Assert.DoesNotContain(result.GeneratedClasses.Values, v => v.EndsWith(".csproj", StringComparison.Ordinal));
        // ...and coverage is exactly model + operation + the always-on base.
        Assert.Equal(
            new[] { "ProcessorBase", "RestoreVolumeProcessor", "Widget" },
            result.GeneratedClasses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Output_paths_follow_the_convention()
    {
        // THE single place that pins the *incidental* output path scheme. A rename
        // of the layout/suffix should touch only this test; everything else asserts
        // the stable contract (class coverage, parseability, content).
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["restoreVolume"]));

        Assert.Equal("Models/Widget.cs", result.GeneratedClasses["Widget"]);
        Assert.Equal("Processors/RestoreVolumeProcessor.cs", result.GeneratedClasses["RestoreVolumeProcessor"]);
        Assert.Equal("Processors/ProcessorBase.cs", result.GeneratedClasses["ProcessorBase"]);
        Assert.Contains("Contoso.Clients.csproj", writer.Files.Keys);
    }

    [Fact]
    public async Task Generate_ProducesParseableCSharp()
    {
        (GenerationResult result, ArtifactWriter writer) =
            await RunAsync(Ir(models: ["Widget"], operations: ["get"]));

        // Content contract: the model type exists in its API namespace (looked up
        // by class name, not by a hardcoded path).
        string model = writer.Files.Source(result, "Widget");
        Assert.Contains("namespace Contoso.Clients.Models", model);
        Assert.Contains("class Widget", model);

        // Every generated C# source must be valid C# (no syntax-error diagnostics).
        foreach ((string _, string source) in writer.Files.CSharpSources())
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public async Task Generate_ReportsWarningForEmptyModelName()
    {
        (GenerationResult result, _) = await RunAsync(Ir(models: [""]));

        Assert.Contains(result.Diagnostics, d => d.Code == "ace-generator/empty-model-name");
        // The empty-named model is skipped, not emitted — only the always-on base remains.
        Assert.Equal("ProcessorBase", Assert.Single(result.GeneratedClasses.Keys));
    }

    [Fact]
    public async Task Generate_NamesBothModelsWhenTwoRenderAsOneType()
    {
        // Distinct models routinely share a name across namespaces (`ErrorResponse`
        // exists in a provider's namespace and in Azure.ResourceManager.CommonTypes).
        // They collapse onto one C# type, and since a model reference resolves by the
        // id's name, references to either bind to whichever was emitted first — silent,
        // and it compiles. The diagnostic must name both ids, not just the file.
        TransformedIR ir = Ir();
        ir.Models =
        [
            new IRModel
            {
                Name = "ErrorResponse",
                Source = "Test",
                Id = new IRId { Module = "Test", Namespace = ["Contoso", "Storage"], Name = "ErrorResponse" },
            },
            new IRModel
            {
                Name = "ErrorResponse",
                Source = "Common",
                Id = new IRId { Module = "Common", Namespace = ["Azure", "CommonTypes"], Name = "ErrorResponse" },
            },
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        GeneratorDiagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            d => d.Code == "ace-generator/model-name-collision");
        Assert.Contains("Contoso.Storage.ErrorResponse", diagnostic.Message);
        Assert.Contains("Azure.CommonTypes.ErrorResponse", diagnostic.Message);

        // The second is dropped at the source, so the generic duplicate-file warning
        // never fires — the specific diagnostic replaces it rather than doubling it.
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "ace-generator/duplicate-file");
        Assert.Single(writer.Files.Keys, k => k.EndsWith("ErrorResponse.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_ReferencesARenamedModelByItsCurrentName()
    {
        // A transform renames a model to keep two same-named types apart, which makes
        // `model.Name` diverge from `id.Name`. A reference resolved off the id would
        // then name a type nobody declares — so references must resolve through the
        // document. Nothing in the IR itself flags the divergence, which is why this is
        // pinned rather than left to a real spec to expose.
        IRId renamedId = new() { Module = "Common", Namespace = ["Azure", "CommonTypes"], Name = "ErrorResponse" };
        TransformedIR ir = Ir();
        ir.Models =
        [
            new IRModel
            {
                Name = "CommonTypesErrorResponse",
                Source = "Common",
                Id = renamedId,
            },
            new IRModel
            {
                Name = "Holder",
                Source = "Test",
                Id = new IRId { Module = "Test", Name = "Holder" },
                Properties =
                [
                    new IRPropertyObject
                    {
                        Id = new IRId { Module = "Test", Name = "Holder.error" },
                        Name = "Error",
                        WireName = "error",
                        Type = new IRTypeRefModel { Id = renamedId },
                        Optional = true,
                        Nullable = false,
                    },
                ],
            },
        ];

        (GenerationResult result, ArtifactWriter writer) = await RunAsync(ir);

        Assert.True(result.Success);
        string holder = writer.Files.Source(result, "Holder");
        Assert.Contains("CommonTypesErrorResponse? Error", holder);
        // The id's name must not leak through as a type reference.
        Assert.DoesNotContain("ErrorResponse? Error", holder.Replace("CommonTypesErrorResponse? Error", ""));
    }

    [Fact]
    public async Task Generate_EmitsOnlyRelativeForwardSlashPaths()
    {
        (_, ArtifactWriter writer) = await RunAsync(Ir(models: ["Widget"], operations: ["get"]));

        Assert.NotEmpty(writer.Files);
        foreach (string path in writer.Files.Keys)
        {
            Assert.False(path.StartsWith('/'), $"'{path}' must be relative");
            Assert.False(path.Contains('\\'), $"'{path}' must use '/' separators");
            Assert.False(path.Contains(".."), $"'{path}' must not escape the base directory");
        }
    }
}
