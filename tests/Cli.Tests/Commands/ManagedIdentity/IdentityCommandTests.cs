// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;

namespace Cli.Tests.Commands.ManagedIdentity;

/// <summary>
/// Runs managed identity CLI commands through the real engine and checks the error for each rejected input. The wire
/// mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class IdentityCommandTests : IDisposable
{
    private const string ExtensionName = "ManagedServiceIdentity";
    private const string MissingIdentityAddressMessage = "Provide either --ids, or both --name and --resource-group.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public IdentityCommandTests() => _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── identity show / update / delete ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("identity", "show")]
    [InlineData("identity", "update")]
    [InlineData("identity", "delete")]
    public async Task RunAsync_NameWithoutResourceGroup_ThrowsInvalidInput(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--name", "id"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(MissingIdentityAddressMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_ShowBlankName_ThrowsInvalidInput()
    {
        string blankName = " ";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "--name", blankName, "--resource-group", "rg"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(MissingIdentityAddressMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("identity", "show")]
    [InlineData("identity", "update")]
    [InlineData("identity", "delete")]
    public async Task RunAsync_InvalidResourceId_ThrowsInvalidInput(params string[] commandLine)
    {
        string resourceGroupId = "/subscriptions/x/resourceGroups/rg";
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--ids", resourceGroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Invalid managed identity resource id '/subscriptions/x/resourceGroups/rg'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{resourceName}.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_ShowBlankId_ThrowsInvalidInput()
    {
        string blankId = " ";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "--ids", blankId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Identity resource id cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── identity create / update tags and resource restriction ─────────────────────────────────

    [Theory]
    [InlineData("identity", "create", "--name", "id", "--resource-group", "rg", "--location", "westus", "--tags", "env")]
    [InlineData("identity", "update", "--name", "id", "--resource-group", "rg", "--tags", "env")]
    public async Task RunAsync_TagWithoutValue_ThrowsInvalidInput(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid dictionary entry 'env'. Expected KEY=VALUE format.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CreateTagWithBlankKey_ThrowsInvalidInput()
    {
        string tagWithBlankKey = " =test";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "--name", "id", "--resource-group", "rg", "--location", "westus", "--tags", tagWithBlankKey]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid dictionary entry ' =test'. Keys cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CreateJsonTagWithBlankKey_ThrowsInvalidInput()
    {
        string jsonTagWithBlankKey = "{\"\":\"test\"}";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "--name", "id", "--resource-group", "rg", "--location", "westus", "--tags", jsonTagWithBlankKey]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid dictionary entry. Keys cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // Current behaviour: malformed JSON tags surface the raw System.Text.Json error instead of a usage error.
    [Fact]
    public async Task RunAsync_CreateMalformedJsonTags_ThrowsJsonException()
    {
        string malformedJsonTags = "{bad";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "--name", "id", "--resource-group", "rg", "--location", "westus", "--tags", malformedJsonTags]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "'b' is an invalid start of a property name. Expected a '\"'. Path: $ | LineNumber: 0 | BytePositionInLine: 1.",
            Assert.IsType<JsonException>(exception).Message);
    }

    // Current behaviour: a malformed JSON resource restriction surfaces the raw System.Text.Json reader error
    // (an internal JsonException subtype, hence IsAssignableFrom) instead of a usage error.
    [Theory]
    [InlineData("identity", "create", "--name", "id", "--resource-group", "rg", "--location", "westus", "--resource-restriction", "{bad")]
    [InlineData("identity", "update", "--name", "id", "--resource-group", "rg", "--resource-restriction", "{bad")]
    public async Task RunAsync_MalformedJsonResourceRestriction_ThrowsJsonException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "'b' is an invalid start of a property name. Expected a '\"'. LineNumber: 0 | BytePositionInLine: 1.",
            Assert.IsAssignableFrom<JsonException>(exception).Message);
    }
}
