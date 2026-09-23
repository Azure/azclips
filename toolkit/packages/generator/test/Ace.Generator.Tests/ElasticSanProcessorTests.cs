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
        Action<TransformedIR>? configure = null)
    {
        var writer = new ArtifactWriter(scenario);
        TransformedIR ir = LoadIr();
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
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_Get");

        string get = writer.Files.Source(result, "GetElasticSansProcessor");

        Assert.Contains("public partial class GetElasticSansProcessor : ProcessorBase<object, ElasticSan>", get);
        Assert.Contains("Method => HttpMethod.Get;", get);
        Assert.DoesNotContain("CreateRequestBody", get);
        Assert.DoesNotContain("SendAsyncLRO", get);
    }

    [Fact]
    public async Task Emits_PowerShell_Metadata_From_Canonical_Command_Dialects()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_PS_Metadata", ir =>
        {
            IRCommand command = ir.Commands.Single(command => command.Name == "ListByResourceGroupElasticSans");
            command.Summary = "Lists Elastic SAN resources.";
            command.Docs = "Lists Elastic SAN resources in a resource group.";
            command.Dialect = new IRCommandDialect
            {
                Ps = new PsCommandDialect
                {
                    Verb = "Get",
                    Noun = "AzElasticSan",
                    SupportsShouldProcess = true,
                    ConfirmImpact = PsCommandDialectConfirmImpact.High,
                    Aliases = ["Get-AzElasticSans"],
                    Description = "Lists Elastic SAN resources for PowerShell.",
                    Examples = ["Get-AzElasticSan -ResourceGroup demo"],
                    LifecycleTag = PsCommandDialectLifecycleTag.Preview,
                    HelpUri = "https://aka.ms/Get-AzElasticSan",
                    Completer = new PSCompleterAttribute { Script = "Get-ElasticSanCommand" },
                },
            };

            IRCommandParameter resourceGroup = command.Parameters.Single(parameter => parameter.Name == "ResourceGroupName");
            resourceGroup.Dialect = new IRCommandParameterDialect
            {
                Ps = new PsParameterDialect
                {
                    Name = "ResourceGroup",
                    Aliases = ["ResourceGroupName"],
                    Description = "Resource group to enumerate.",
                    ParameterSets =
                    [
                        new PsParameterSetMembership
                        {
                            Set = null,
                            Mandatory = false,
                        },
                        new PsParameterSetMembership
                        {
                            Set = "ByResourceGroup",
                            Mandatory = true,
                            Position = 0,
                            ValueFromPipeline = true,
                            ValueFromPipelineByPropertyName = true,
                        },
                    ],
                    Completer = new PSCompleterAttribute { Name = "ResourceGroupCompleter" },
                    DefaultValue = new PSDefaultValueAttribute
                    {
                        Script = "$env:AZURE_RESOURCE_GROUP",
                        Name = "AZURE_RESOURCE_GROUP",
                        Description = "Default resource group.",
                        SetCondition = "$null -eq $ResourceGroup",
                    },
                },
            };

            command.Parameters.Single(parameter => parameter.Name == "SubscriptionId").Dialect =
                new IRCommandParameterDialect
                {
                    Ps = new PsParameterDialect
                    {
                        ParameterSets = [new PsParameterSetMembership { Set = "Other", Mandatory = true }],
                    },
                };
        });

        string list = writer.Files.Source(result, "ListByResourceGroupElasticSansProcessor");

        Assert.Contains("using Core.Abstractions;", list);
        Assert.Contains("[PSCommand(\"Get\", \"AzElasticSan\", \"ByResourceGroup\"", list);
        Assert.Contains("SupportsShouldProcess = true", list);
        Assert.Contains("ConfirmImpact = PSConfirmImpact.High", list);
        Assert.Contains("Aliases = new string[] { \"Get-AzElasticSans\" }", list);
        Assert.Contains("Summary = \"Lists Elastic SAN resources.\"", list);
        Assert.Contains("Description = \"Lists Elastic SAN resources for PowerShell.\"", list);
        Assert.Contains("Examples = new string[] { \"Get-AzElasticSan -ResourceGroup demo\" }", list);
        Assert.Contains("Tags = new HelpTag[] { HelpTag.Preview }", list);
        Assert.Contains("HttpPath = \"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ElasticSan/elasticSans\"", list);
        Assert.Contains("ApiVersion = \"2024-07-01-preview\"", list);
        Assert.Contains("SupportsPaging = true", list);
        Assert.Contains("HelpUri = \"https://aka.ms/Get-AzElasticSan\"", list);
        Assert.Contains("OutputTypes = new Type[] { typeof(ElasticSanList) }", list);
        Assert.Contains("[PSParameterSet(\"Get-AzElasticSan\", \"ByResourceGroup\")]", list);
        Assert.Contains("[PSCompleter(\"Get-AzElasticSan\", Script = \"Get-ElasticSanCommand\")]", list);
        Assert.Contains("[PSParameter(\"Get-AzElasticSan\", \"ByResourceGroup\", \"ResourceGroup\", Mandatory = true", list);
        Assert.Contains("HelpMessage = \"Resource group to enumerate.\"", list);
        Assert.Contains("Position = 0", list);
        Assert.Contains("ValueFromPipeline = true", list);
        Assert.Contains("ValueFromPipelineByPropertyName = true", list);
        Assert.Contains("Aliases = new string[] { \"ResourceGroupName\" }", list);
        Assert.Contains("[PSCompleter(\"Get-AzElasticSan\", ParameterSetName = \"ByResourceGroup\", Name = \"ResourceGroupCompleter\")]", list);
        Assert.Contains("[PSDefaultValue(\"Get-AzElasticSan\", \"ByResourceGroup\", Script = \"$env:AZURE_RESOURCE_GROUP\"", list);
        Assert.DoesNotContain("[PSParameter(\"Get-AzElasticSan\", \"ByResourceGroup\", \"SubscriptionId\"", list);
        Assert.Contains("[PSParameter(\"Get-AzElasticSan\", \"ByResourceGroup\", \"Accept\", Mandatory = true, Hidden = true)]", list);
        Assert.DoesNotContain("IsIdentity", list);
        Assert.DoesNotContain("PSIdentity", list);
        Assert.DoesNotContain("PSModule", list);

        Assert.Contains("public partial class ListByResourceGroupElasticSansProcessor : ProcessorBase<object, ElasticSanList>", list);
        Assert.Contains("Method => HttpMethod.Get;", list);
        Assert.DoesNotContain("SendAsyncLRO", list);
        Assert.Contains("IEnumerable<ElasticSan>? pageItems(ElasticSanList body) => body.Value;", list);
        Assert.Contains("string? nextLink(ElasticSanList body) => body.NextLink;", list);
        Assert.Contains("BeforeWriteOutputAsyncAndReturn(ElasticSanList? body)", list);
        Assert.Contains("body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions)", list);
    }

    [Fact]
    public async Task Emits_Completed_PowerShell_Command_And_Parameter_Metadata()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_PS_Completed", ir =>
        {
            IRCommand command = ir.Commands.Single(command => command.Name == "ListByResourceGroupElasticSans");
            command.Dialect = new IRCommandDialect
            {
                Ps = new PsCommandDialect
                {
                    Verb = "Get",
                    Noun = "AzElasticSan",
                    DefaultParameterSet = "FamilyDefault",
                    SupportsPaging = false,
                    HttpPath = new PsCommandDialectHttpPath
                    {
                        Path = "/custom/{ResourceGroupName}",
                        ApiVersion = "2099-01-01",
                    },
                    OutputTypes = [command.Surface.Response!],
                },
            };

            IRCommandParameter identity = command.Parameters.Single(parameter => parameter.Name == "ResourceGroupName");
            var identityId = new IRId { Module = "ElasticSan", Name = "ElasticSanIdentity" };
            identity.Source = new IRCommandParameterSourceSynthetic
            {
                Purpose = "identity",
                Type = identityId,
                Parameters = [ir.Operations[0].Id],
            };
            identity.Dialect = new IRCommandParameterDialect
            {
                Ps = new PsParameterDialect
                {
                    ParameterSets = [new PsParameterSetMembership { Set = "ByResourceGroup", Mandatory = true }],
                },
            };
            command.Parameters.Single(parameter => parameter.Name == "SubscriptionId").Dialect =
                new IRCommandParameterDialect { Ps = new PsParameterDialect { Removed = true } };
        });

        string list = writer.Files.Source(result, "ListByResourceGroupElasticSansProcessor");
        Assert.Contains("[PSCommand(\"Get\", \"AzElasticSan\", \"FamilyDefault\"", list);
        Assert.Contains("HttpPath = \"/custom/{ResourceGroupName}\"", list);
        Assert.Contains("ApiVersion = \"2099-01-01\"", list);
        Assert.Contains("SupportsPaging = false", list);
        Assert.Contains("[PSParameterSet(\"Get-AzElasticSan\", \"ByResourceGroup\")]", list);
        Assert.Contains("[PSParameter(\"Get-AzElasticSan\", \"ByResourceGroup\", \"ResourceGroupName\", Mandatory = true", list);
        Assert.Contains("IsIdentity = true", list);
        Assert.DoesNotContain("[PSParameter(\"Get-AzElasticSan\", \"ByResourceGroup\", \"SubscriptionId\"", list);
    }

    [Fact]
    public async Task Emits_PowerShell_Module_And_Identity_Artifacts()
    {
        string sourceId = string.Empty;
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_PS_Module", ir =>
        {
            sourceId = ir.Sources[0].Id;
            IRCommand command = ir.Commands.Single(candidate => candidate.Name == "ListByResourceGroupElasticSans");
            command.Dialect = new IRCommandDialect
            {
                Ps = new PsCommandDialect { Verb = "Get", Noun = "AzElasticSan" },
            };
            IRParameter path = ir.Operations.SelectMany(operation => operation.PathParameters)
                .First(parameter => string.Equals(
                    parameter.Name,
                    "resourceGroupName",
                    StringComparison.OrdinalIgnoreCase));
                    var identityId = new IRId { Module = "ElasticSan", Name = "ElasticSanIdentity" };
            ir.Target.Dialect = new IRTargetDialect
            {
                Ps = new IRTargetDialectPs
                {
                    Name = "ElasticSan",
                    Description = "Elastic SAN module.",
                    Prefix = "Az",
                    SubjectPrefix = "ElasticSan",
                },
            };
            ir.SharedTypes =
            [
                new TransformedIRSharedTypes
                {
                    Kind = "identity",
                    Id = identityId,
                    Name = "ElasticSanIdentity",
                    Properties =
                    [
                        new TransformedIRSharedTypesProperties
                        {
                            Name = "ResourceGroupName",
                            Description = "Resource group identity.",
                            SourceParameters = [path.Id],
                        },
                        new TransformedIRSharedTypesProperties
                        {
                            Name = "ResourceId",
                            SourceParameters = [],
                        },
                    ],
                },
            ];
        });

        string assembly = writer.Files["Properties/PowerShellAssemblyInfo.cs"];
        Assert.Contains($"[assembly: PSModule(\"ElasticSan\", \"{sourceId}\"", assembly);
        Assert.Contains("Description = \"Elastic SAN module.\"", assembly);
        Assert.Contains("CmdletsToExport = new string[] { \"Get-AzElasticSan\" }", assembly);

        string identity = writer.Files.Source(result, "ElasticSanIdentity");
        Assert.Contains("[PSIdentity(\"ElasticSanIdentity\")]", identity);
        Assert.Contains("[PSIdentityProperty(\"ResourceGroupName\", HelpMessage = \"Resource group identity.\")]", identity);
        Assert.Contains("public string? ResourceGroupName { get; set; }", identity);
        Assert.Contains("public string? ResourceId { get; set; }", identity);
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(assembly).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(identity).GetDiagnostics(),
            diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Emits_A_Shared_Identity_Without_Module_Metadata()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_PS_IdentityOnly", ir =>
        {
            ir.Target.Dialect = null;
            ir.SharedTypes =
            [
                new TransformedIRSharedTypes
                {
                    Kind = "identity",
                    Id = new IRId { Module = "ElasticSan", Name = "ElasticSanIdentity" },
                    Name = "ElasticSanIdentity",
                    Properties = [],
                },
            ];
        });

        Assert.DoesNotContain("Properties/PowerShellAssemblyInfo.cs", writer.Files.Keys);
        Assert.Contains("[PSIdentity(\"ElasticSanIdentity\")]", writer.Files.Source(result, "ElasticSanIdentity"));
    }

    [Fact]
    public async Task Emits_PowerShell_Metadata_On_Flat_Body_Parameters_But_Not_Body()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_PS_Body_Metadata", ir =>
        {
            IRCommand command = ir.Commands.Single(command => command.Name == "CreateElasticSans");
            command.Dialect = new IRCommandDialect
            {
                Ps = new PsCommandDialect
                {
                    Verb = "New",
                    Noun = "AzElasticSan",
                    DefaultParameterSet = "Create",
                },
            };
            command.Parameters.Single(parameter => parameter.Name == "SkuName").Dialect =
                new IRCommandParameterDialect
                {
                    Ps = new PsParameterDialect { Name = "Sku", Aliases = ["SkuName"] },
                };
        });

        string create = writer.Files.Source(result, "CreateElasticSansProcessor");

        Assert.Contains("[PSParameter(\"New-AzElasticSan\", \"Create\", \"Sku\", Mandatory = true", create);
        Assert.Contains("Aliases = new string[] { \"SkuName\" }", create);
        Assert.Contains("[PSParameter(\"New-AzElasticSan\", \"Create\", \"Location\", Mandatory = true", create);
        Assert.DoesNotContain("[PSParameter(\"New-AzElasticSan\", \"Create\", \"Body\"", create);
    }

    [Fact]
    public async Task Omits_PowerShell_Attributes_And_Using_When_Dialect_Is_Absent()
    {
        (GenerationResult result, ArtifactWriter writer) = await EmitAsync("ElasticSan_Processors_No_PS_Dialect");

        string get = writer.Files.Source(result, "GetElasticSansProcessor");

        Assert.DoesNotContain("using Core.Abstractions;", get);
        Assert.DoesNotContain("[PS", get);
    }
}
