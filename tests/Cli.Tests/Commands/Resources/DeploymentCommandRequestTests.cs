// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs deployment CLI commands at resource group, subscription, management group and tenant scope through the real
/// engine and checks the requests each one sends to ARM. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class DeploymentCommandRequestTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string OtherSubscriptionId = "11111111-2222-3333-4444-555555555555";
    private const string GroupName = "rg";
    private const string ManagementGroupId = "mg1";
    private const string DeploymentName = "d1";
    private const string SecondDeploymentName = "d2";
    private const string DerivedDeploymentName = "t";
    private const string PreviousDeploymentName = "prev";
    private const string Location = "westus";
    private const string TemplateUri = "https://example.com/t.json";
    private const string TemplateSpecId = "/subscriptions/x/templateSpecs/ts/versions/1";
    private const string QueryString = "?sv=1";
    private const string ParametersUri = "https://example.com/p.json";
    private const string NextPageToken = "p2";
    private const string FailedFilter = "provisioningState eq 'Failed'";
    private const string ApiVersion = "2025-04-01";
    private const string GroupDeployments = $"/subscriptions/{SubscriptionId}/resourcegroups/{GroupName}/providers/Microsoft.Resources/deployments";
    private const string SubDeployments = $"/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments";
    private const string MgDeployments = $"/providers/Microsoft.Management/managementGroups/{ManagementGroupId}/providers/Microsoft.Resources/deployments";
    private const string TenantDeployments = "/providers/Microsoft.Resources/deployments";
    private const string GroupDeployment = $"{GroupDeployments}/{DeploymentName}";
    private const string SubDeployment = $"{SubDeployments}/{DeploymentName}";
    private const string MgDeployment = $"{MgDeployments}/{DeploymentName}";
    private const string TenantDeployment = $"{TenantDeployments}/{DeploymentName}";
    private const string OtherSubscriptionGroup = $"/subscriptions/{OtherSubscriptionId}/resourcegroups/{GroupName}";
    private const string OtherSubscriptionGroupDeployment = $"{OtherSubscriptionGroup}/providers/Microsoft.Resources/deployments/{DeploymentName}";
    private const string OperationStatus = $"{GroupDeployment}/operationStatuses/op1";
    private const string OperationResults = $"/subscriptions/{SubscriptionId}/operationresults";
    private const string GroupDeploymentUrl = ArmUrl + GroupDeployment;
    private const string TenantDeploymentUrl = ArmUrl + TenantDeployment;
    private const string WhatIfBody = """{"status":"Succeeded","properties":{"changes":[]}}""";
    private const string GroupDeploymentBody = $$$"""{"id":"{{{GroupDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded","mode":"Incremental"}}""";
    private const string AcceptedGroupDeploymentBody = $$$"""{"id":"{{{GroupDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Accepted","mode":"Incremental"}}""";
    private const string TenantDeploymentBody = $$$"""{"id":"{{{TenantDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded"}}""";
    private const string AcceptedTenantDeploymentBody = $$$"""{"id":"{{{TenantDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Accepted"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public DeploymentCommandRequestTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each command that sends one request, with the method, the path, a response status and body.
    public static TheoryData<string[], string, string, HttpStatusCode, string?> GetSingleRequestCommands()
    {
        (string Scope, string[] ScopeArgs, string[] LocationArgs, string Deployments)[] scopes =
        [
            ("group", ["--resource-group", GroupName], [], GroupDeployments),
            ("sub", [], ["--location", Location], SubDeployments),
            ("mg", ["--management-group-id", ManagementGroupId], ["--location", Location], MgDeployments),
            ("tenant", [], ["--location", Location], TenantDeployments),
        ];
        TheoryData<string[], string, string, HttpStatusCode, string?> data = new();
        foreach ((string scope, string[] scopeArgs, string[] locationArgs, string deployments) in scopes)
        {
            string deployment = $"{deployments}/{DeploymentName}";
            string body = $$$"""{"id":"{{{deployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded","mode":"Incremental"}}""";
            data.Add(
                ["deployment", scope, "validate", .. scopeArgs, "--name", DeploymentName, .. locationArgs, "--template-uri", TemplateUri],
                "POST", $"{deployment}/validate", HttpStatusCode.OK, body);
            data.Add(
                ["deployment", scope, "what-if", .. scopeArgs, "--name", DeploymentName, .. locationArgs, "--template-uri", TemplateUri],
                "POST", $"{deployment}/whatIf", HttpStatusCode.OK, WhatIfBody);
            data.Add(["deployment", scope, "show", .. scopeArgs, "--name", DeploymentName], "GET", deployment, HttpStatusCode.OK, body);
            data.Add(["deployment", scope, "list", .. scopeArgs], "GET", deployments, HttpStatusCode.OK, EmptyList);
            data.Add(["deployment", scope, "cancel", .. scopeArgs, "--name", DeploymentName], "POST", $"{deployment}/cancel", HttpStatusCode.NoContent, null);
            data.Add(["deployment", scope, "delete", .. scopeArgs, "--name", DeploymentName], "DELETE", deployment, HttpStatusCode.NoContent, null);
            data.Add(
                ["deployment", scope, "export", .. scopeArgs, "--name", DeploymentName],
                "POST", $"{deployment}/exportTemplate", HttpStatusCode.OK, """{"template":{"resources":[]}}""");
        }

        return data;
    }

    // Each create command, its deployments path and the requests it sends when the PUT completes at once.
    // Current behaviour: a PUT that answers 200 Succeeded is still polled and then re-read with a final GET.
    public static TheoryData<string[], string, string[]> GetCreateCommands() => new()
    {
        {
            ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri],
            GroupDeployments,
            [
                $"POST {GroupDeployment}/validate?api-version={ApiVersion}",
                $"PUT {GroupDeployment}?api-version={ApiVersion}",
                $"GET {GroupDeployment}?api-version={ApiVersion}",
                $"GET {GroupDeployment}?api-version={ApiVersion}",
            ]
        },
        {
            // Current behaviour: unlike the other scopes, a subscription deployment is not validated before the PUT.
            ["deployment", "sub", "create", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri],
            SubDeployments,
            [
                $"PUT {SubDeployment}?api-version={ApiVersion}",
                $"GET {SubDeployment}?api-version={ApiVersion}",
                $"GET {SubDeployment}?api-version={ApiVersion}",
            ]
        },
        {
            [
                "deployment", "mg", "create",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
            ],
            MgDeployments,
            [
                $"POST {MgDeployment}/validate?api-version={ApiVersion}",
                $"PUT {MgDeployment}?api-version={ApiVersion}",
                $"GET {MgDeployment}?api-version={ApiVersion}",
                $"GET {MgDeployment}?api-version={ApiVersion}",
            ]
        },
        {
            ["deployment", "tenant", "create", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri],
            TenantDeployments,
            [
                $"POST {TenantDeployment}/validate?api-version={ApiVersion}",
                $"PUT {TenantDeployment}?api-version={ApiVersion}",
                $"GET {TenantDeployment}?api-version={ApiVersion}",
                $"GET {TenantDeployment}?api-version={ApiVersion}",
            ]
        },
    };

    // Each scope's create command without the template, its deployments path and the PUT body it sends with --template-uri.
    public static TheoryData<string[], string, string> GetCreateScopes() => new()
    {
        {
            ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName],
            GroupDeployments,
            """{"properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}"""
        },
        {
            ["deployment", "sub", "create", "--name", DeploymentName, "--location", Location],
            SubDeployments,
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}"""
        },
        {
            ["deployment", "mg", "create", "--management-group-id", ManagementGroupId, "--name", DeploymentName, "--location", Location],
            MgDeployments,
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}"""
        },
        {
            ["deployment", "tenant", "create", "--name", DeploymentName, "--location", Location],
            TenantDeployments,
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}"""
        },
    };

    // Each what-if command that asks for full resource payloads, with the body it sends.
    public static TheoryData<string[], string> GetFullResourcePayloadsWhatIfCommands() => new()
    {
        {
            ["deployment", "group", "what-if", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri],
            """{"properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"FullResourcePayloads"}}}"""
        },
        {
            [
                "deployment", "mg", "what-if",
                "--management-group-id", ManagementGroupId,
                "--location", Location,
                "--name", DeploymentName,
                "--template-uri", TemplateUri,
            ],
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"FullResourcePayloads"}}}"""
        },
        {
            ["deployment", "tenant", "what-if", "--location", Location, "--name", DeploymentName, "--template-uri", TemplateUri],
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"FullResourcePayloads"}}}"""
        },
    };

    // Each scope's what-if command asking for the ResourceIdOnly result format, with the body it sends.
    public static TheoryData<string[], string> GetResourceIdOnlyWhatIfCommands() => new()
    {
        {
            [
                "deployment", "group", "what-if",
                "--resource-group", GroupName,
                "--name", DeploymentName,
                "--template-uri", TemplateUri,
                "--result-format", "ResourceIdOnly",
            ],
            """{"properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"ResourceIdOnly"}}}"""
        },
        {
            ["deployment", "sub", "what-if", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri, "-r", "ResourceIdOnly"],
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"ResourceIdOnly"}}}"""
        },
        {
            [
                "deployment", "mg", "what-if",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
                "--result-format", "ResourceIdOnly",
            ],
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"ResourceIdOnly"}}}"""
        },
        {
            [
                "deployment", "tenant", "what-if",
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
                "--result-format", "ResourceIdOnly",
            ],
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental","whatIfSettings":{"resultFormat":"ResourceIdOnly"}}}"""
        },
    };

    // Each scope's list command.
    public static TheoryData<string[]> GetListCommands() => new()
    {
        { ["deployment", "group", "list", "--resource-group", GroupName] },
        { ["deployment", "sub", "list"] },
        { ["deployment", "mg", "list", "--management-group-id", ManagementGroupId] },
        { ["deployment", "tenant", "list"] },
    };

    // ── every deployment command ────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetSingleRequestCommands))]
    public async Task RunAsync_DeploymentCommand_SendsOneRequestWithDeploymentsApiVersion(
        string[] commandLine, string method, string path, HttpStatusCode status, string? responseBody)
    {
        _mockWire.RespondTo(new HttpMethod(method), ArmUrl + path, status, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{method} {path}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── deployment <scope> create ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetCreateCommands))]
    public async Task RunAsync_DeploymentCreate_SendsValidatePutAndCompletionGets(string[] commandLine, string deployments, string[] expectedRequests)
    {
        RespondToImmediateCreate(deployments, DeploymentName);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(expectedRequests, _mockWire.FullRequestLines());
    }

    [Theory]
    [MemberData(nameof(GetCreateScopes))]
    public async Task RunAsync_DeploymentCreateTemplateUri_SendsTemplateLinkPutBody(string[] commandLine, string deployments, string expectedBody)
    {
        List<string?> putBodies = RespondToImmediateCreate(deployments, DeploymentName).PutBodies;
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(expectedBody, Assert.Single(putBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateCompleteWithRollback_SendsValidateBodyEqualToPutBody()
    {
        (List<string?> validateBodies, List<string?> putBodies) = RespondToImmediateCreate(GroupDeployments, DeploymentName);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", GroupName,
                "--name", DeploymentName,
                "--template-uri", TemplateUri,
                "--mode", "Complete",
                "--rollback-on-error",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(Assert.Single(putBodies), Assert.Single(validateBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateWithoutName_SendsTemplateUriFileNameAsDeploymentName()
    {
        const string Deployment = $"{GroupDeployments}/{DerivedDeploymentName}";
        RespondToImmediateCreate(GroupDeployments, DerivedDeploymentName);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", GroupName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {Deployment}/validate", $"PUT {Deployment}", $"GET {Deployment}", $"GET {Deployment}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateAllBodyOptions_SendsEveryPropertyInPutBody()
    {
        List<string?> putBodies = RespondToImmediateCreate(GroupDeployments, DeploymentName).PutBodies;
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", GroupName,
                "--name", DeploymentName,
                "--template-spec-id", TemplateSpecId,
                "--mode", "Complete",
                "--parameter", "a=b", "n=5",
                "--parameter", """{"o":{"value":true}}""",
                "--rollback-on-error",
                "--validation-level", "Provider",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"templateLink":{"id":"/subscriptions/x/templateSpecs/ts/versions/1"},"parameters":"""
                + """{"a":{"value":"b"},"n":{"value":5},"o":{"value":true}},"mode":"Complete","onErrorDeployment":{"type":"LastSuccessful"},"validationLevel":"Provider"}}""",
            Assert.Single(putBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateLinkOptions_SendsQueryStringParametersLinkAndRollbackTarget()
    {
        List<string?> putBodies = RespondToImmediateCreate(GroupDeployments, DeploymentName).PutBodies;
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", GroupName,
                "--name", DeploymentName,
                "--template-uri", TemplateUri,
                "--rollback-to-deployment-name", PreviousDeploymentName,
                "--query-string", QueryString,
                "--parameters-uri", ParametersUri,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"templateLink":{"uri":"https://example.com/t.json","queryString":"sv=1"},"parametersLink":"""
                + """{"uri":"https://example.com/p.json"},"mode":"Incremental","onErrorDeployment":{"type":"SpecificDeployment","deploymentName":"prev"}}}""",
            Assert.Single(putBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentSubCreateShortAliases_SendsParametersModeAndValidationLevel()
    {
        List<string?> putBodies = RespondToImmediateCreate(SubDeployments, DeploymentName).PutBodies;
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "sub", "create",
                "--name", DeploymentName, "-l", Location,
                "--template-uri", TemplateUri,
                "--mode", "Complete", "-p", "a=b",
                "--validation-level", "Template",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"parameters":"""
                + """{"a":{"value":"b"}},"mode":"Complete","validationLevel":"Template"}}""",
            Assert.Single(putBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentMgCreateModeComplete_ValidatesWithIncrementalMode()
    {
        List<string?> validateBodies = RespondToImmediateCreate(MgDeployments, DeploymentName).ValidateBodies;
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "mg", "create",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
                "--mode", "Complete",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}""", Assert.Single(validateBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentMgCreateModeComplete_SendsCompleteModePutBody()
    {
        List<string?> putBodies = RespondToImmediateCreate(MgDeployments, DeploymentName).PutBodies;
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "mg", "create",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
                "--mode", "Complete",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Complete"}}""", Assert.Single(putBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantCreateNoWait_SendsValidateAndPutOnly()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{TenantDeploymentUrl}/validate", HttpStatusCode.OK, TenantDeploymentBody);
        _mockWire.RespondTo(HttpMethod.Put, TenantDeploymentUrl, HttpStatusCode.Created, AcceptedTenantDeploymentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "tenant", "create", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri, "--no-wait"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {TenantDeployment}/validate", $"PUT {TenantDeployment}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateAsyncOperation_PollsOperationStatusThenGetsDeployment()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.OK, GroupDeploymentBody);
        _mockWire.RespondWithHeaders(HttpMethod.Put, GroupDeploymentUrl, HttpStatusCode.Created, AcceptedGroupDeploymentBody,
            $"Azure-AsyncOperation: {ArmUrl}{OperationStatus}?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + OperationStatus, HttpStatusCode.OK, """{"status":"Succeeded"}""");
        _mockWire.RespondTo(HttpMethod.Get, GroupDeploymentUrl, HttpStatusCode.OK, GroupDeploymentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"POST {GroupDeployment}/validate", $"PUT {GroupDeployment}", $"GET {OperationStatus}", $"GET {GroupDeployment}"],
            _mockWire.RequestLines());
    }

    // ── deployment <scope> validate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DeploymentGroupValidate_SendsTemplateLinkBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.OK, GroupDeploymentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "validate", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupValidateAccepted_PollsLocation()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.Accepted, null,
            $"Location: {ArmUrl}{OperationResults}/v1?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{OperationResults}/v1", HttpStatusCode.OK, GroupDeploymentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "validate", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {GroupDeployment}/validate", $"GET {OperationResults}/v1"], _mockWire.RequestLines());
    }

    // ── deployment <scope> what-if ─────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetFullResourcePayloadsWhatIfCommands))]
    public async Task RunAsync_DeploymentWhatIf_SendsFullResourcePayloadsResultFormat(string[] commandLine, string expectedBody)
    {
        List<string?> sentBodies = _mockWire.RespondToAnyUrl(HttpMethod.Post, HttpStatusCode.OK, WhatIfBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(expectedBody, Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentSubWhatIf_SendsNoResultFormat()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{SubDeployment}/whatIf", HttpStatusCode.OK, WhatIfBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "sub", "what-if", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: unlike the other scopes, the subscription what-if leaves the result format to the service.
        Assert.Equal("""{"location":"westus","properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Incremental"}}""", Assert.Single(sentBodies));
    }

    [Theory]
    [MemberData(nameof(GetResourceIdOnlyWhatIfCommands))]
    public async Task RunAsync_DeploymentWhatIfResultFormat_SendsGivenResultFormat(string[] commandLine, string expectedBody)
    {
        List<string?> sentBodies = _mockWire.RespondToAnyUrl(HttpMethod.Post, HttpStatusCode.OK, WhatIfBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(expectedBody, Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupWhatIfModeComplete_SendsCompleteMode()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/whatIf", HttpStatusCode.OK, WhatIfBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "what-if", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri, "--mode", "Complete"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"templateLink":{"uri":"https://example.com/t.json"},"mode":"Complete","whatIfSettings":{"resultFormat":"FullResourcePayloads"}}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupWhatIfAccepted_PollsLocation()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Post, $"{GroupDeploymentUrl}/whatIf", HttpStatusCode.Accepted, null,
            $"Location: {ArmUrl}{OperationResults}/w1?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{OperationResults}/w1", HttpStatusCode.OK, WhatIfBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "what-if", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {GroupDeployment}/whatIf", $"GET {OperationResults}/w1"], _mockWire.RequestLines());
    }

    // ── deployment <scope> show ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DeploymentGroupShowSubscriptionId_SendsGetUnderGivenSubscription()
    {
        _mockWire.RespondTo(
            HttpMethod.Get, ArmUrl + OtherSubscriptionGroupDeployment, HttpStatusCode.OK, GroupDeploymentBody.Replace(SubscriptionId, OtherSubscriptionId));
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "show", "--resource-group", GroupName, "--name", DeploymentName, "--subscription-id", OtherSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {OtherSubscriptionGroupDeployment}"], _mockWire.RequestLines());
    }

    // ── deployment <scope> list ────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetListCommands))]
    public async Task RunAsync_DeploymentListFilter_SendsFilterQuery(string[] commandLine)
    {
        _mockWire.RespondToAnyRequest(EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--filter", FailedFilter]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(FailedFilter, Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupListNextLink_SendsListGetThenNextLinkGet()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"name":"{{DeploymentName}}"}],"nextLink":"{{ArmUrl}}{{GroupDeployments}}?api-version={{ApiVersion}}&$skiptoken={{NextPageToken}}"}""",
            $$"""{"value":[{"name":"{{SecondDeploymentName}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "list", "--resource-group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"GET {GroupDeployments}?api-version={ApiVersion}", $"GET {GroupDeployments}?api-version={ApiVersion}&$skiptoken={NextPageToken}"],
            _mockWire.FullRequestLines());
    }

    // ── deployment <scope> delete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DeploymentGroupDeleteAccepted_PollsLocation()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Delete, GroupDeploymentUrl, HttpStatusCode.Accepted, null,
            $"Location: {ArmUrl}{OperationResults}/x1?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{OperationResults}/x1", HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "delete", "--resource-group", GroupName, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {GroupDeployment}", $"GET {OperationResults}/x1"], _mockWire.RequestLines());
    }

    // ── deployment tenant wait ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--exists", HttpStatusCode.OK, TenantDeploymentBody)]
    [InlineData("--deleted", HttpStatusCode.NotFound, null)]
    [InlineData("--created", HttpStatusCode.OK, TenantDeploymentBody)]
    [InlineData("--updated", HttpStatusCode.OK, TenantDeploymentBody)]
    public async Task RunAsync_DeploymentTenantWaitConditionAlreadyMet_SendsOneGet(string condition, HttpStatusCode status, string? responseBody)
    {
        _mockWire.RespondTo(HttpMethod.Get, TenantDeploymentUrl, status, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "tenant", "wait", "--name", DeploymentName, condition]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {TenantDeployment}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // Answers the validation POST, the PUT (finished, with Retry-After 0 so polling does not wait) and the completion GETs
    // for deployment name under deployments. Returns the bodies sent to the validation POST and to the PUT.
    private (List<string?> ValidateBodies, List<string?> PutBodies) RespondToImmediateCreate(string deployments, string name)
    {
        string body = $$$"""{"id":"{{{deployments}}}/{{{name}}}","name":"{{{name}}}","properties":{"provisioningState":"Succeeded","mode":"Incremental"}}""";
        List<string?> validateBodies = _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{deployments}/{name}/validate", HttpStatusCode.OK, body);
        List<string?> putBodies = _mockWire.RespondWithHeaders(HttpMethod.Put, $"{ArmUrl}{deployments}/{name}", HttpStatusCode.OK, body, "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{deployments}/{name}", HttpStatusCode.OK, body);
        return (validateBodies, putBodies);
    }
}
