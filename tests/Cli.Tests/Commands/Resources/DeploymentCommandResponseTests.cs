// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs deployment CLI commands at resource group, subscription, management group and tenant scope through the real
/// engine against canned ARM responses and checks what each command writes, or the error it throws for a response.
/// </summary>
public sealed class DeploymentCommandResponseTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string GroupName = "rg";
    private const string ManagementGroupId = "mg1";
    private const string DeploymentName = "d1";
    private const string SecondDeploymentName = "d2";
    private const string Location = "westus";
    private const string TemplateUri = "https://example.com/t.json";
    private const string ApiVersion = "2025-04-01";
    private const string GroupDeployments = $"/subscriptions/{SubscriptionId}/resourcegroups/{GroupName}/providers/Microsoft.Resources/deployments";
    private const string SubDeployments = $"/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments";
    private const string MgDeployments = $"/providers/Microsoft.Management/managementGroups/{ManagementGroupId}/providers/Microsoft.Resources/deployments";
    private const string TenantDeployments = "/providers/Microsoft.Resources/deployments";
    private const string GroupDeployment = $"{GroupDeployments}/{DeploymentName}";
    private const string SubDeployment = $"{SubDeployments}/{DeploymentName}";
    private const string MgDeployment = $"{MgDeployments}/{DeploymentName}";
    private const string TenantDeployment = $"{TenantDeployments}/{DeploymentName}";
    private const string OperationStatus = $"{GroupDeployment}/operationStatuses/op1";
    private const string OperationResults = $"/subscriptions/{SubscriptionId}/operationresults";
    private const string GroupDeploymentUrl = ArmUrl + GroupDeployment;
    private const string TenantDeploymentUrl = ArmUrl + TenantDeployment;
    private const string EmptyObject = "{}";
    private const string GroupDeploymentBody = $$$"""{"id":"{{{GroupDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded","mode":"Incremental"}}""";
    private const string AcceptedGroupDeploymentBody = $$$"""{"id":"{{{GroupDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Accepted","mode":"Incremental"}}""";
    private const string TenantDeploymentBody = $$$"""{"id":"{{{TenantDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded"}}""";
    private const string AcceptedTenantDeploymentBody = $$$"""{"id":"{{{TenantDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Accepted"}}""";
    private const string WhatIfCreateBody = """{"status":"Succeeded","properties":{"changes":[{"resourceId":"/r1","changeType":"Create"}]}}""";
    private const string ValidationErrorBody = """{"error":{"code":"InvalidTemplate","message":"Bad template"}}""";
    private const string FailedWhatIfBody = """{"status":"Failed","error":{"code":"WhatIfFailed","message":"bad"}}""";
    private const string DeploymentNotFoundBody = $$$"""{"error":{"code":"DeploymentNotFound","message":"Deployment '{{{DeploymentName}}}' could not be found."}}""";
    private const string DeletedBody = """{"status":"Deleted"}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public DeploymentCommandResponseTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each scope's create command and its deployment path.
    public static TheoryData<string[], string> GetCreateCommands() => new()
    {
        { ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri], GroupDeployment },
        { ["deployment", "sub", "create", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri], SubDeployment },
        {
            [
                "deployment", "mg", "create",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
            ],
            MgDeployment
        },
        { ["deployment", "tenant", "create", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri], TenantDeployment },
    };

    // Each scope's validate command and its deployment path.
    public static TheoryData<string[], string> GetValidateCommands() => new()
    {
        { ["deployment", "group", "validate", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri], GroupDeployment },
        { ["deployment", "sub", "validate", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri], SubDeployment },
        {
            [
                "deployment", "mg", "validate",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
            ],
            MgDeployment
        },
        { ["deployment", "tenant", "validate", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri], TenantDeployment },
    };

    // Each validate command whose scope formats a validation error.
    public static TheoryData<string[]> GetErrorFormattingValidateCommands() => new()
    {
        { ["deployment", "group", "validate", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri] },
        {
            [
                "deployment", "mg", "validate",
                "--management-group-id", ManagementGroupId,
                "--location", Location,
                "--name", DeploymentName,
                "--template-uri", TemplateUri,
            ]
        },
        { ["deployment", "tenant", "validate", "--location", Location, "--name", DeploymentName, "--template-uri", TemplateUri] },
    };

    // Each scope's what-if command and its deployment path.
    public static TheoryData<string[], string> GetWhatIfCommands() => new()
    {
        { ["deployment", "group", "what-if", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri], GroupDeployment },
        { ["deployment", "sub", "what-if", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri], SubDeployment },
        {
            [
                "deployment", "mg", "what-if",
                "--management-group-id", ManagementGroupId,
                "--name", DeploymentName,
                "--location", Location,
                "--template-uri", TemplateUri,
            ],
            MgDeployment
        },
        { ["deployment", "tenant", "what-if", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri], TenantDeployment },
    };

    // Each what-if command whose scope throws when the what-if status is Failed.
    public static TheoryData<string[]> GetThrowingWhatIfCommands() => new()
    {
        { ["deployment", "group", "what-if", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri] },
        {
            [
                "deployment", "mg", "what-if",
                "--management-group-id", ManagementGroupId,
                "--location", Location,
                "--name", DeploymentName,
                "--template-uri", TemplateUri,
            ]
        },
        { ["deployment", "tenant", "what-if", "--location", Location, "--name", DeploymentName, "--template-uri", TemplateUri] },
    };

    // Each scope's command taking only the scope and the deployment name, for verb, with its deployment path.
    public static TheoryData<string[], string> GetNamedCommands(string verb) => new()
    {
        { ["deployment", "group", verb, "--resource-group", GroupName, "--name", DeploymentName], GroupDeployment },
        { ["deployment", "sub", verb, "--name", DeploymentName], SubDeployment },
        { ["deployment", "mg", verb, "--management-group-id", ManagementGroupId, "--name", DeploymentName], MgDeployment },
        { ["deployment", "tenant", verb, "--name", DeploymentName], TenantDeployment },
    };

    // Each scope's list command and its deployments path.
    public static TheoryData<string[], string> GetListCommands() => new()
    {
        { ["deployment", "group", "list", "--resource-group", GroupName], GroupDeployments },
        { ["deployment", "sub", "list"], SubDeployments },
        { ["deployment", "mg", "list", "--management-group-id", ManagementGroupId], MgDeployments },
        { ["deployment", "tenant", "list"], TenantDeployments },
    };

    // Each delete command that drops the body of a 200 response, with its deployment path.
    public static TheoryData<string[], string> GetBodyDroppingDeleteCommands() => new()
    {
        { ["deployment", "group", "delete", "--resource-group", GroupName, "--name", DeploymentName], GroupDeployment },
        { ["deployment", "sub", "delete", "--name", DeploymentName], SubDeployment },
    };

    // Each delete command that writes the body of a 200 response, with its deployment path.
    public static TheoryData<string[], string> GetBodyWritingDeleteCommands() => new()
    {
        { ["deployment", "mg", "delete", "--management-group-id", ManagementGroupId, "--name", DeploymentName], MgDeployment },
        { ["deployment", "tenant", "delete", "--name", DeploymentName], TenantDeployment },
    };

    // Each export command that throws for a response without a template, with the message it throws.
    public static TheoryData<string[], string> GetThrowingExportCommands() => new()
    {
        {
            ["deployment", "group", "export", "--resource-group", GroupName, "--name", DeploymentName],
            "The export response for deployment 'd1' in resource group 'rg' did not contain a template."
        },
        {
            ["deployment", "tenant", "export", "--name", DeploymentName],
            "The export response for deployment 'd1' at tenant scope did not contain a template."
        },
    };

    // Each export command that writes null for a response without a template.
    public static TheoryData<string[]> GetNullWritingExportCommands() => new()
    {
        { ["deployment", "sub", "export", "--name", DeploymentName] },
        { ["deployment", "mg", "export", "--management-group-id", ManagementGroupId, "--name", DeploymentName] },
    };

    // Each tenant wait condition argument list met by a deployment in provisioningState.
    public static TheoryData<string[], string> GetMetWaitConditions() => new()
    {
        { ["--created"], "Succeeded" },
        { ["--exists"], "Running" },
        { ["--custom", "properties.provisioningState=='Running'"], "Running" },
    };

    // ── deployment <scope> create ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetCreateCommands))]
    public async Task RunAsync_DeploymentCreateRunningPut_WritesSucceededDeploymentFromGet(string[] commandLine, string deployment)
    {
        RespondToCreate(deployment, "Succeeded");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("Succeeded", Assert.IsType<DeploymentExtended>(Assert.Single(_output)).Properties?.ProvisioningState);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateAsyncOperation_WritesDeploymentAfterOperationSucceeds()
    {
        RespondToGroupValidate();
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

        Assert.Equal("Succeeded", Assert.IsType<DeploymentExtended>(Assert.Single(_output)).Properties?.ProvisioningState);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateFailed_ThrowsLongRunningOperationFailed()
    {
        RespondToCreate(GroupDeployment, "Failed");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        // Current behaviour: the polling runtime throws a plain Exception carrying the whole response, not the deployment error.
        Assert.StartsWith("Long running operation failed with response: StatusCode: 200", Assert.IsType<Exception>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateValidationError_ThrowsValidationErrorWithTargetAndDetails()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.BadRequest,
            """{"error":{"code":"InvalidTemplate","message":"Bad template","target":"res1","details":[{"code":"Inner","message":"inner msg"}]}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Deployment validation failed.{Environment.NewLine}  InvalidTemplate: Bad template (target: res1){Environment.NewLine}    Inner: inner msg",
            Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreatePutBadRequest_ThrowsStatusUrlAndBody()
    {
        const string ErrorBody = """{"error":{"code":"Bad","message":"nope"}}""";
        RespondToGroupValidate();
        _mockWire.RespondTo(HttpMethod.Put, GroupDeploymentUrl, HttpStatusCode.BadRequest, ErrorBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"400 Bad Request from {GroupDeploymentUrl}?api-version={ApiVersion}: {ErrorBody}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantCreateNoWait_WritesAcceptedDeployment()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{TenantDeploymentUrl}/validate", HttpStatusCode.OK, TenantDeploymentBody);
        _mockWire.RespondTo(HttpMethod.Put, TenantDeploymentUrl, HttpStatusCode.Created, AcceptedTenantDeploymentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "tenant", "create", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri, "--no-wait"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("Accepted", Assert.IsType<DeploymentExtended>(Assert.Single(_output)).Properties?.ProvisioningState);
    }

    // ── deployment <scope> validate ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetValidateCommands))]
    public async Task RunAsync_DeploymentValidate_WritesValidateResult(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{deployment}/validate", HttpStatusCode.OK,
            $$$"""{"id":"{{{deployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded"}}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(DeploymentName, Assert.IsType<DeploymentValidateResult>(Assert.Single(_output)).Name);
    }

    [Theory]
    [MemberData(nameof(GetErrorFormattingValidateCommands))]
    public async Task RunAsync_DeploymentValidateBadRequestWithError_ThrowsValidationFailedWithErrorCode(string[] commandLine)
    {
        _mockWire.RespondToAnyUrl(HttpMethod.Post, HttpStatusCode.BadRequest, ValidationErrorBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"Deployment validation failed.{Environment.NewLine}  InvalidTemplate: Bad template", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentSubValidateBadRequestWithError_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{SubDeployment}/validate", HttpStatusCode.BadRequest, ValidationErrorBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "sub", "validate", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        // Current behaviour: unlike the other scopes, the subscription validate does not format the validation error.
        Assert.Equal(
            $"400 Bad Request from {ArmUrl}{SubDeployment}/validate?api-version={ApiVersion}: {ValidationErrorBody}",
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupValidateBadRequestWithoutError_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.BadRequest, """{"x":1}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "validate", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($$"""400 Bad Request from {{GroupDeploymentUrl}}/validate?api-version={{ApiVersion}}: {"x":1}""", Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupValidateNoContent_WritesNothing()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "validate", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupValidateAccepted_WritesPolledValidateResult()
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

        Assert.Equal(DeploymentName, Assert.IsType<DeploymentValidateResult>(Assert.Single(_output)).Name);
    }

    // ── deployment <scope> what-if ─────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetWhatIfCommands))]
    public async Task RunAsync_DeploymentWhatIf_WritesChangedResourceId(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{deployment}/whatIf", HttpStatusCode.OK, WhatIfCreateBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("/r1", Assert.Single(Assert.IsType<WhatIfOperationResult>(Assert.Single(_output)).Properties!.Changes!).ResourceId);
    }

    [Theory]
    [MemberData(nameof(GetThrowingWhatIfCommands))]
    public async Task RunAsync_DeploymentWhatIfStatusFailed_ThrowsWhatIfFailedWithErrorCode(string[] commandLine)
    {
        _mockWire.RespondToAnyRequest(FailedWhatIfBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"What-if operation failed.{Environment.NewLine}  WhatIfFailed: bad", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentSubWhatIfStatusFailed_WritesFailedResult()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{SubDeployment}/whatIf", HttpStatusCode.OK, FailedWhatIfBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "sub", "what-if", "--name", DeploymentName, "--location", Location, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: unlike the other scopes, a failed subscription what-if is written instead of thrown.
        Assert.Equal("Failed", Assert.IsType<WhatIfOperationResult>(Assert.Single(_output)).Status);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupWhatIfAccepted_WritesPolledResult()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Post, $"{GroupDeploymentUrl}/whatIf", HttpStatusCode.Accepted, null,
            $"Location: {ArmUrl}{OperationResults}/w1?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{OperationResults}/w1", HttpStatusCode.OK, """{"status":"Succeeded","properties":{"changes":[]}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "what-if", "--resource-group", GroupName, "--name", DeploymentName, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("Succeeded", Assert.IsType<WhatIfOperationResult>(Assert.Single(_output)).Status);
    }

    // ── deployment <scope> show ────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetNamedCommands), "show")]
    public async Task RunAsync_DeploymentShow_WritesDeploymentId(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + deployment, HttpStatusCode.OK,
            $$$"""{"id":"{{{deployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Succeeded"}}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(deployment, Assert.IsType<DeploymentExtended>(Assert.Single(_output)).Id);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupShowNotFound_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupDeploymentUrl, HttpStatusCode.NotFound, DeploymentNotFoundBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "show", "--resource-group", GroupName, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"404 Not Found from {GroupDeploymentUrl}?api-version={ApiVersion}: {DeploymentNotFoundBody}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── deployment <scope> list ────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetListCommands))]
    public async Task RunAsync_DeploymentList_WritesDeploymentNames(string[] commandLine, string deployments)
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + deployments, HttpStatusCode.OK,
            $$"""{"value":[{"id":"{{deployments}}/{{DeploymentName}}","name":"{{DeploymentName}}"},"""
                + $$"""{"id":"{{deployments}}/{{SecondDeploymentName}}","name":"{{SecondDeploymentName}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([DeploymentName, SecondDeploymentName], Assert.IsType<List<DeploymentExtended>>(Assert.Single(_output)).Select(d => d.Name));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupListNextLink_WritesEveryPage()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"{{GroupDeployment}}","name":"{{DeploymentName}}"}],"nextLink":"{{ArmUrl}}{{GroupDeployments}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            $$"""{"value":[{"id":"{{GroupDeployments}}/{{SecondDeploymentName}}","name":"{{SecondDeploymentName}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "list", "--resource-group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([DeploymentName, SecondDeploymentName], Assert.IsType<List<DeploymentExtended>>(Assert.Single(_output)).Select(d => d.Name));
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupListEmpty_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + GroupDeployments, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "list", "--resource-group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<DeploymentExtended>>(Assert.Single(_output)));
    }

    // ── deployment <scope> cancel ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetNamedCommands), "cancel")]
    public async Task RunAsync_DeploymentCancelNoContent_WritesNothing(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{deployment}/cancel", HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCancelWithBody_WritesResponseJson()
    {
        const string CanceledBody = """{"status":"Canceled"}""";
        _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/cancel", HttpStatusCode.OK, CanceledBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "cancel", "--resource-group", GroupName, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(CanceledBody, Assert.IsType<JsonElement>(Assert.Single(_output)).GetRawText());
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCancelConflict_ThrowsStatusUrlAndBody()
    {
        const string ErrorBody = """{"error":{"code":"DeploymentCannotBeCancelled","message":"Already finished."}}""";
        _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/cancel", HttpStatusCode.Conflict, ErrorBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "cancel", "--resource-group", GroupName, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"409 Conflict from {GroupDeploymentUrl}/cancel?api-version={ApiVersion}: {ErrorBody}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── deployment <scope> delete ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetNamedCommands), "delete")]
    public async Task RunAsync_DeploymentDeleteNoContent_WritesNothing(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Delete, ArmUrl + deployment, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Theory]
    [MemberData(nameof(GetBodyDroppingDeleteCommands))]
    public async Task RunAsync_DeploymentDeleteOkWithBody_WritesNothing(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Delete, ArmUrl + deployment, HttpStatusCode.OK, DeletedBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: a 200 delete body is dropped at group and subscription scope but written at the other scopes.
        Assert.Empty(_output);
    }

    [Theory]
    [MemberData(nameof(GetBodyWritingDeleteCommands))]
    public async Task RunAsync_DeploymentDeleteOkWithBody_WritesResponseJson(string[] commandLine, string deployment)
    {
        _mockWire.RespondTo(HttpMethod.Delete, ArmUrl + deployment, HttpStatusCode.OK, DeletedBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(DeletedBody, Assert.IsType<JsonElement>(Assert.Single(_output)).GetRawText());
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupDeleteAccepted_WritesNothingAfterPolling()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Delete, GroupDeploymentUrl, HttpStatusCode.Accepted, null,
            $"Location: {ArmUrl}{OperationResults}/x1?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{OperationResults}/x1", HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "group", "delete", "--resource-group", GroupName, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    // ── deployment <scope> export ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetNamedCommands), "export")]
    public async Task RunAsync_DeploymentExport_WritesTemplateOnly(string[] commandLine, string deployment)
    {
        const string Template = """{"$schema":"s","resources":[]}""";
        const string ExportBody = $$$"""{"template":{{{Template}}}}""";
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{deployment}/exportTemplate", HttpStatusCode.OK, ExportBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(Template, Assert.IsType<JsonElement>(Assert.Single(_output)).GetRawText());
    }

    [Theory]
    [MemberData(nameof(GetThrowingExportCommands))]
    public async Task RunAsync_DeploymentExportWithoutTemplate_ThrowsMissingTemplate(string[] commandLine, string expectedMessage)
    {
        _mockWire.RespondToAnyRequest(EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(expectedMessage, Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetNullWritingExportCommands))]
    public async Task RunAsync_DeploymentExportWithoutTemplate_WritesNull(string[] commandLine)
    {
        _mockWire.RespondToAnyRequest(EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: sub and mg export write null when the template is missing, while group and tenant throw.
        Assert.Equal(new SerializedJson("null"), Assert.Single(_output));
    }

    // ── deployment tenant wait ─────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetMetWaitConditions))]
    public async Task RunAsync_DeploymentTenantWaitConditionMet_WritesNothing(string[] conditionArgs, string provisioningState)
    {
        _mockWire.RespondTo(HttpMethod.Get, TenantDeploymentUrl, HttpStatusCode.OK,
            $$$"""{"id":"{{{TenantDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"{{{provisioningState}}}"}}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "tenant", "wait", "--name", DeploymentName, .. conditionArgs]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitDeletedNotFound_WritesNothing()
    {
        _mockWire.RespondTo(HttpMethod.Get, TenantDeploymentUrl, HttpStatusCode.NotFound, DeploymentNotFoundBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "tenant", "wait", "--name", DeploymentName, "--deleted"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitCreatedFailed_ThrowsOperationFailed()
    {
        _mockWire.RespondTo(HttpMethod.Get, TenantDeploymentUrl, HttpStatusCode.OK,
            $$$"""{"id":"{{{TenantDeployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"Failed"}}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "tenant", "wait", "--name", DeploymentName, "--created"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The operation failed", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitForbidden_ThrowsStatusUrlAndBody()
    {
        const string ErrorBody = """{"error":{"code":"AuthorizationFailed","message":"no"}}""";
        _mockWire.RespondTo(HttpMethod.Get, TenantDeploymentUrl, HttpStatusCode.Forbidden, ErrorBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "tenant", "wait", "--name", DeploymentName, "--created"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"403 Forbidden from {TenantDeploymentUrl}?api-version={ApiVersion}: {ErrorBody}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    // Answers the validation POST, the PUT (200 Running, with Retry-After 0 so polling does not wait) and the polling
    // and final GETs for deployment, which report provisioningState.
    private void RespondToCreate(string deployment, string provisioningState)
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}{deployment}/validate", HttpStatusCode.OK, DeploymentBody(deployment, "Succeeded"));
        _mockWire.RespondWithHeaders(HttpMethod.Put, ArmUrl + deployment, HttpStatusCode.OK, DeploymentBody(deployment, "Running"), "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + deployment, HttpStatusCode.OK, DeploymentBody(deployment, provisioningState));
    }

    private void RespondToGroupValidate() => _mockWire.RespondTo(HttpMethod.Post, $"{GroupDeploymentUrl}/validate", HttpStatusCode.OK, GroupDeploymentBody);

    private static string DeploymentBody(string deployment, string provisioningState) =>
        $$$"""{"id":"{{{deployment}}}","name":"{{{DeploymentName}}}","properties":{"provisioningState":"{{{provisioningState}}}","mode":"Incremental"}}""";
}
