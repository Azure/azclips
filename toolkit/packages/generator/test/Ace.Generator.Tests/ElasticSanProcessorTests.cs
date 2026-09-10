using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Emits the real ElasticSan IR (TestData/elasticsan.ir.json) and checks the
/// <em>processor</em> output. Faithful "garbage in, garbage out" emission — we assert one
/// processor per operation, that every file is syntactically valid C#, and spot-check the
/// request shape (base generic, URI, body, LRO send override). Output lands in
/// <c>artifacts/ElasticSan_Processors/</c> for eyeballing.
/// The historical capture's semantics were migrated to IR 0.11.0. Newly introduced
/// declaration/SDK flags use fixture defaults; they are not a re-emission of the source.
/// </summary>
public sealed class ElasticSanProcessorTests
{
    private static readonly string IrPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "elasticsan.ir.json");

    private static TransformedIR LoadIr() => IrJson.Deserialize(File.ReadAllText(IrPath));

    private static async Task<(GenerationResult Result, ArtifactWriter Writer)> EmitAsync(
        string scenario,
        Action<IRDocument>? configure = null)
    {
        var writer = new ArtifactWriter(scenario);
        IRDocument ir = LoadIr();
        configure?.Invoke(ir);
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            new GeneratorOptions { RootNamespace = "Azure.ElasticSan" },
            writer.WriteAsync);
        return (result, writer);
    }

    [Fact]
    public async Task Emits_A_Processor_File_Per_Command()
    {
        TransformedIR ir = LoadIr();
        (_, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors");

        // One processor per command, plus the shared ProcessorBase.
        Assert.Equal(ir.Commands.Count + 1, writer.Files.ProcessorSources().Count());
    }

    [Fact]
    public async Task Emitted_Processors_Are_Parseable()
    {
        (_, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_Parse");

        foreach ((string _, string source) in writer.Files.ProcessorSources())
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            Assert.DoesNotContain(
                tree.GetDiagnostics(),
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public async Task Commands_Are_Named_From_Their_Subject_And_Action()
    {
        (GenerationResult result, _) = await EmitAsync("ElasticSan_Processors_Interfaces");

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        foreach (string name in new[]
        {
            // No @clientLocation applies (its decorator is scoped "go"), so this one
            // keeps the subject derived from its declaration site.
            "RestoreVolumeElasticSanProcessor",
            // Relocated by the spec: VolumeGroups.preBackup -> Volumes.
            "PreBackupVolumesProcessor",
            "PreRestoreVolumesProcessor",
            // ElasticSans.listByElasticSan -> "PrivateLinkResources".
            "ListByElasticSanPrivateLinkResourcesProcessor",
            // Snapshots.get -> "VolumeSnapshots".
            "GetVolumeSnapshotsProcessor",
            // SkusOperationGroup.list -> "Skus", dropping the interface's suffix.
            "ListSkusProcessor",
        })
        {
            Assert.Contains(name, result.GeneratedClasses.Keys);
        }
    }

    [Fact]
    public async Task Create_Processor_Has_Body_Uri_And_Lro_Send()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_Create");

        string create = writer.Files.Source(result, "CreateElasticSansProcessor");

        Assert.Contains("public partial class CreateElasticSansProcessor : ProcessorBase<ElasticSan, ElasticSan>", create);
        // Members mirror the API path parameters, subscriptionId inline (not special-cased).
        Assert.Contains("public string SubscriptionId { get; set; }", create);
        Assert.Contains("public string ResourceGroupName { get; set; }", create);
        Assert.Contains("public string ElasticSanName { get; set; }", create);
        // The body is projected onto flat members, so the payload leaves the public surface —
        // otherwise it would duplicate every one of them.
        Assert.Contains("internal ElasticSan? Body { get; set; }", create);
        Assert.Contains("CreateRequestBody() => Body;", create);
        // Request seams: PUT method + interpolated URI (host, path members, api-version).
        Assert.Contains("Method => HttpMethod.Put;", create);
        // The endpoint is not baked in: it interpolates the base's seam, defaulted from IR.
        Assert.Contains("{Endpoint}/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ElasticSan/elasticSans/{ElasticSanName}?api-version=", create);
        // Long-running -> final-state hook (strict from IR finalStateVia=location) + send override.
        Assert.Contains("GetFinalUri => response => response.GetFirstHeader(\"Location\");", create);
        Assert.Contains("SendAsync() => SendAsyncLRO();", create);
    }

    [Fact]
    public async Task Get_Processor_Has_No_Body_And_No_Lro_Send()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_Get", ir =>
        {
            IROperation operation = ir.Operations.Single(operation =>
                operation.Name == "get"
                && operation.Id.Interface == "ElasticSans");
            IRParameter name = operation.PathParameters.Single(parameter => parameter.Name == "elasticSanName");
            ir.Ps = new PS
            {
                Commands =
                [
                    new PSCommandAttribute
                    {
                        Verb = "Get",
                        Noun = "AzElasticSan",
                        Subject = "ElasticSan",
                        DefaultParameterSetName = "Get",
                        OutputTypes = [((IRTypeRefModel)operation.Responses.First(response => response.Type is IRTypeRefModel).Type!).Id],
                        SourceOperationIds = [operation.Id],
                        ParameterSets =
                        [
                            new PSParameterSetAttribute { Name = "Get", SourceOperationId = operation.Id },
                            new PSParameterSetAttribute { Name = "GetByPipeline", SourceOperationId = operation.Id },
                            new PSParameterSetAttribute { Name = "GetViaIdentity", SourceOperationId = operation.Id },
                        ],
                        Parameters =
                        [
                            new PSParameterAttribute
                            {
                                Name = "Name",
                                SourceParameterId = name.Id,
                                ParameterSets = new Dictionary<string, PSParameterSetOverride>
                                {
                                    ["Get"] = new() { Mandatory = true, ValueFromPipeline = true },
                                    ["GetByPipeline"] = new() { ValueFromPipelineByPropertyName = true },
                                },
                            },
                            new PSParameterAttribute
                            {
                                Name = "InputObject",
                                Mandatory = true,
                                ParameterSetName = "GetViaIdentity",
                                Identity = true,
                            },
                        ],
                    },
                ],
            };
        });

        string get = writer.Files.Source(result, "GetElasticSansProcessor");

        Assert.Contains("[PSCommand(\"Get\", \"AzElasticSan\", \"Get\"", get);
        Assert.Contains("[PSParameterSet(\"Get-AzElasticSan\", \"Get\")]", get);
        Assert.Contains("[PSParameterSet(\"Get-AzElasticSan\", \"GetByPipeline\")]", get);
        Assert.Contains("[PSParameterSet(\"Get-AzElasticSan\", \"GetViaIdentity\")]", get);
        Assert.Contains("[PSParameter(\"Get-AzElasticSan\", \"Get\", \"Name\", Mandatory = true, ValueFromPipeline = true", get);
        Assert.Contains("[PSParameter(\"Get-AzElasticSan\", \"GetByPipeline\", \"Name\", ValueFromPipelineByPropertyName = true", get);
        Assert.DoesNotContain("[PSParameter(\"Get-AzElasticSan\", \"GetViaIdentity\", \"Name\"", get);
        Assert.Contains("public partial class GetElasticSansProcessor : ProcessorBase<object, ElasticSan>", get);
        Assert.Contains("Method => HttpMethod.Get;", get);
        Assert.DoesNotContain("CreateRequestBody", get);
        Assert.DoesNotContain("SendAsyncLRO", get);
    }

    [Fact]
    public async Task List_Processor_Has_Pagination_Wiring()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_List");

        string list = writer.Files.Source(result, "ListByResourceGroupElasticSansProcessor");

        // Pageable -> a plain GET (no LRO) that wires the runtime pagination helper.
        Assert.Contains("public partial class ListByResourceGroupElasticSansProcessor : ProcessorBase<object, ElasticSanList>", list);
        Assert.Contains("Method => HttpMethod.Get;", list);
        Assert.DoesNotContain("SendAsyncLRO", list);
        // Selectors from IR response-body bindings (PascalCased) + the runtime helper override.
        Assert.Contains("IEnumerable<ElasticSan>? pageItems(ElasticSanList body) => body.Value;", list);
        Assert.Contains("string? nextLink(ElasticSanList body) => body.NextLink;", list);
        Assert.Contains("BeforeWriteOutputAsyncAndReturn(ElasticSanList? body)", list);
        Assert.Contains("body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions)", list);
    }
}
