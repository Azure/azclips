// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs deployment operation CLI commands at resource group, subscription, management group and tenant scope through the
/// real engine against canned ARM responses and checks what each command writes, or the error it throws for a response.
/// </summary>
public sealed class DeploymentOperationCommandResponseTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string ResourceGroup = "rg";
    private const string ManagementGroupId = "mg1";
    private const string DeploymentName = "d1";
    private const string OperationId = "op1";
    private const string SecondOperationId = "op2";
    private const string ApiVersion = "2026-06-01";
    private const string GroupOperations = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroup}/deployments/{DeploymentName}/operations";
    private const string SubOperations = $"/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments/{DeploymentName}/operations";
    private const string MgOperations = $"/providers/Microsoft.Management/managementGroups/{ManagementGroupId}/providers/Microsoft.Resources/deployments/{DeploymentName}/operations";
    private const string TenantOperations = $"/providers/Microsoft.Resources/deployments/{DeploymentName}/operations";
    private const string GroupOperationsUrl = ArmUrl + GroupOperations;
    private const string GroupOperationBody = $$"""{"id":"{{GroupOperations}}/{{OperationId}}","operationId":"{{OperationId}}"}""";
    private const string SecondGroupOperationBody = $$"""{"id":"{{GroupOperations}}/{{SecondOperationId}}","operationId":"{{SecondOperationId}}"}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public DeploymentOperationCommandResponseTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each scope's operation command for verb, with the scope and deployment arguments, and the operations path.
    public static TheoryData<string[], string> GetOperationCommands(string verb) => new()
    {
        { ["deployment", "operation", "group", verb, "--resource-group", ResourceGroup, "--name", DeploymentName], GroupOperations },
        { ["deployment", "operation", "sub", verb, "--name", DeploymentName], SubOperations },
        { ["deployment", "operation", "mg", verb, "--management-group-id", ManagementGroupId, "--name", DeploymentName], MgOperations },
        { ["deployment", "operation", "tenant", verb, "--name", DeploymentName], TenantOperations },
    };

    // ── deployment operation <scope> list ──────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetOperationCommands), "list")]
    public async Task RunAsync_DeploymentOperationList_WritesOperationIds(string[] commandLine, string operations)
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + operations, HttpStatusCode.OK,
            $$"""{"value":[{"id":"{{operations}}/{{OperationId}}","operationId":"{{OperationId}}"},{"id":"{{operations}}/{{SecondOperationId}}","operationId":"{{SecondOperationId}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["op1", "op2"], Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output)).Select(o => o.OperationId));
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupList_WritesProvisioningState()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupOperationsUrl, HttpStatusCode.OK,
            $$$"""{"value":[{"id":"{{{GroupOperations}}}/{{{OperationId}}}","operationId":"{{{OperationId}}}","properties":{"provisioningState":"Succeeded"}}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "operation", "group", "list", "--resource-group", ResourceGroup, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("Succeeded", Assert.Single(Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output))).Properties?.ProvisioningState);
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupListNextLink_WritesEveryPage()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{{GroupOperationBody}}],"nextLink":"{{GroupOperationsUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            $$"""{"value":[{{SecondGroupOperationBody}}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "operation", "group", "list", "--resource-group", ResourceGroup, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["op1", "op2"], Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output)).Select(o => o.OperationId));
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupListEmpty_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupOperationsUrl, HttpStatusCode.OK, """{"value":[]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "operation", "group", "list", "--resource-group", ResourceGroup, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output)));
    }

    // ── deployment operation <scope> show ──────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetOperationCommands), "show")]
    public async Task RunAsync_DeploymentOperationShow_WritesListWithOperation(string[] commandLine, string operations)
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{operations}/{OperationId}", HttpStatusCode.OK,
            $$"""{"id":"{{operations}}/{{OperationId}}","operationId":"{{OperationId}}"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--operation-ids", OperationId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(OperationId, Assert.Single(Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output))).OperationId);
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupShowTwoIds_WritesOperationsInIdOrder()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupOperationsUrl}/{OperationId}", HttpStatusCode.OK, GroupOperationBody);
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupOperationsUrl}/{SecondOperationId}", HttpStatusCode.OK, SecondGroupOperationBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "operation", "group", "show",
                "--resource-group", ResourceGroup,
                "--name", DeploymentName,
                "--operation-ids", OperationId, SecondOperationId,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([OperationId, SecondOperationId], Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output)).Select(o => o.OperationId));
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupShowNoContent_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupOperationsUrl}/{OperationId}", HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "operation", "group", "show", "--resource-group", ResourceGroup, "--name", DeploymentName, "--operation-ids", OperationId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<DeploymentOperation>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupShowNotFound_ThrowsStatusUrlAndBody()
    {
        const string ErrorBody = $$$"""{"error":{"code":"OperationNotFound","message":"Operation '{{{OperationId}}}' could not be found."}}""";
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupOperationsUrl}/{OperationId}", HttpStatusCode.NotFound, ErrorBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "operation", "group", "show", "--resource-group", ResourceGroup, "--name", DeploymentName, "--operation-ids", OperationId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"404 Not Found from {GroupOperationsUrl}/{OperationId}?api-version={ApiVersion}: {ErrorBody}", Assert.IsType<HttpRequestException>(exception).Message);
    }
}
