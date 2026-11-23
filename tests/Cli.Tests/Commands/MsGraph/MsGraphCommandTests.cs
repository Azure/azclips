// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;

namespace Cli.Tests.Commands.MsGraph;

/// <summary>
/// Runs MsGraph CLI commands through the real engine and checks the error for each rejected input. The wire mock is
/// strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class MsGraphCommandTests : IDisposable
{
    private const string ExtensionName = "MsGraph";
    private const string GroupId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string MemberId = "dddddddd-0000-0000-0000-000000000004";
    private const string GroupIdentifierRequiredMessage = "A group identifier is required. Provide Group or GroupDisplayName.";
    private const string GroupRequiredMessage = "Group is required.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public MsGraphCommandTests() => _engine = CreateEngine(typeof(MsGraphIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── group show ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupShowBlankGroup_ThrowsInvalidInput()
    {
        string blankGroup = "";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "--group", blankGroup]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(GroupRequiredMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── group member check ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupMemberCheckWithoutGroupOrMemberId_ThrowsMissingRequiredArgumentForBoth()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "check"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Invalid arguments: Required parameter '--group' is missing.{Environment.NewLine}Required parameter '--member-id' is missing.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupMemberCheckBlankGroup_ThrowsInvalidInput()
    {
        string blankGroup = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", blankGroup, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(GroupRequiredMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupMemberCheckBlankMemberId_ThrowsInvalidInput()
    {
        string blankMemberId = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupId, "--member-id", blankMemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("MemberId is required to check group membership.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── group member list ──────────────────────────────────────────────────────────────────────
    // Current behaviour: these messages name the PowerShell parameters (Group, GroupDisplayName) even for CLI users.

    [Fact]
    public async Task RunAsync_GroupMemberListGroupAndDisplayName_ThrowsInvalidInput()
    {
        string groupDisplayName = "Sales";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupId, "--display-name", groupDisplayName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Provide either Group or GroupDisplayName, but not both.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupMemberListWithoutGroup_ThrowsInvalidInput()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(GroupIdentifierRequiredMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupMemberListBlankGroup_ThrowsInvalidInput()
    {
        string blankGroup = "";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", blankGroup]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(GroupIdentifierRequiredMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupMemberListBlankDisplayName_ThrowsInvalidInput()
    {
        string blankDisplayName = "";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--display-name", blankDisplayName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(GroupIdentifierRequiredMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }
}
