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
/// Runs deployment operation CLI commands at resource group, subscription, management group and tenant scope through the
/// real engine and checks the requests each one sends to ARM. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class DeploymentOperationCommandRequestTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string GroupName = "rg";
    private const string ManagementGroupId = "mg1";
    private const string DeploymentName = "d1";
    private const string OperationId = "op1";
    private const string SecondOperationId = "op2";
    private const string ApiVersion = "2026-06-01";
    private const string NextPageToken = "p2";
    private const string GroupOperations = $"/subscriptions/{SubscriptionId}/resourcegroups/{GroupName}/deployments/{DeploymentName}/operations";
    private const string SubOperations = $"/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments/{DeploymentName}/operations";
    private const string MgOperations = $"/providers/Microsoft.Management/managementGroups/{ManagementGroupId}/providers/Microsoft.Resources/deployments/{DeploymentName}/operations";
    private const string TenantOperations = $"/providers/Microsoft.Resources/deployments/{DeploymentName}/operations";
    private const string GroupOperationsUrl = ArmUrl + GroupOperations;
    private const string GroupOperationBody = $$"""{"id":"{{GroupOperations}}/{{OperationId}}","operationId":"{{OperationId}}"}""";
    private const string SecondGroupOperationBody = $$"""{"id":"{{GroupOperations}}/{{SecondOperationId}}","operationId":"{{SecondOperationId}}"}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public DeploymentOperationCommandRequestTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each scope's operation command for verb, with the scope and deployment arguments, and the operations path.
    public static TheoryData<string[], string> GetOperationCommands(string verb) => new()
    {
        { ["deployment", "operation", "group", verb, "--resource-group", GroupName, "--name", DeploymentName], GroupOperations },
        { ["deployment", "operation", "sub", verb, "--name", DeploymentName], SubOperations },
        { ["deployment", "operation", "mg", verb, "--management-group-id", ManagementGroupId, "--name", DeploymentName], MgOperations },
        { ["deployment", "operation", "tenant", verb, "--name", DeploymentName], TenantOperations },
    };

    // ── deployment operation <scope> list ──────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetOperationCommands), "list")]
    public async Task RunAsync_DeploymentOperationList_SendsGetToOperationsPath(string[] commandLine, string operations)
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + operations, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {operations}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupListShortAliases_SendsGetToOperationsPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupOperationsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "operation", "group", "list", "-g", GroupName, "-n", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupOperations}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupListNextLink_SendsListGetThenNextLinkGet()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{{GroupOperationBody}}],"nextLink":"{{GroupOperationsUrl}}?api-version={{ApiVersion}}&$skiptoken={{NextPageToken}}"}""",
            $$"""{"value":[{{SecondGroupOperationBody}}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "operation", "group", "list", "--resource-group", GroupName, "--name", DeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"GET {GroupOperations}?api-version={ApiVersion}", $"GET {GroupOperations}?api-version={ApiVersion}&$skiptoken={NextPageToken}"],
            _mockWire.FullRequestLines());
    }

    // ── deployment operation <scope> show ──────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetOperationCommands), "show")]
    public async Task RunAsync_DeploymentOperationShow_SendsGetToOperationPath(string[] commandLine, string operations)
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}{operations}/{OperationId}", HttpStatusCode.OK,
            $$"""{"id":"{{operations}}/{{OperationId}}","operationId":"{{OperationId}}"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--operation-ids", OperationId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {operations}/{OperationId}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_DeploymentOperationGroupShowTwoIds_SendsGetPerOperation()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupOperationsUrl}/{OperationId}", HttpStatusCode.OK, GroupOperationBody);
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupOperationsUrl}/{SecondOperationId}", HttpStatusCode.OK, SecondGroupOperationBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "operation", "group", "show",
                "--resource-group", GroupName,
                "--name", DeploymentName,
                "--operation-ids", OperationId, SecondOperationId,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupOperations}/{OperationId}", $"GET {GroupOperations}/{SecondOperationId}"], _mockWire.RequestLines());
    }
}
