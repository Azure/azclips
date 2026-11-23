// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsMsGraph;

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using GetAzMsGraphGroupCmdlet = CmdletsMsGraph::Microsoft.Azure.Azclips.Cmdlets.MsGraph.GetAzMsGraphGroupCmdlet;
using GetAzMsGraphGroupMemberCmdlet = CmdletsMsGraph::Microsoft.Azure.Azclips.Cmdlets.MsGraph.GetAzMsGraphGroupMemberCmdlet;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;

namespace Cmdlets.Tests.Commands.MsGraph;

/// <summary>
/// Runs MsGraph cmdlets in PowerShell through the real engine and checks the error for each rejected input. The wire
/// mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class MsGraphCommandTests : IDisposable
{
    private const string GroupId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string GroupIdentifierRequiredMessage = "A group identifier is required. Provide Group or GroupDisplayName.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public MsGraphCommandTests()
    {
        _engine = CreateEngine(typeof(MsGraphIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── Get-AzMsGraphGroup ─────────────────────────────────────────────────────────────────────
    // PowerShell rejects an empty value for a mandatory string while binding, so only a whitespace group reaches the
    // extension's checks in this file.

    // Current behaviour: the message names the extension's Group parameter, not ObjectId.
    [Fact]
    public void Invoke_GetGroupWhitespaceObjectId_ThrowsInvalidInput()
    {
        string whitespaceObjectId = " ";
        using PowerShell shell = CreateShell<GetAzMsGraphGroupCmdlet>();
        shell.AddParameter("ObjectId", whitespaceObjectId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Group is required.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzMsGraphGroupMember ───────────────────────────────────────────────────────────────
    // Current behaviour: the message names the extension's Group parameter, not GroupObjectId.

    [Fact]
    public void Invoke_GetGroupMemberWhitespaceGroupObjectId_ThrowsInvalidInput()
    {
        string whitespaceGroupObjectId = " ";
        using PowerShell shell = CreateShell<GetAzMsGraphGroupMemberCmdlet>();
        shell.AddParameter("GroupObjectId", whitespaceGroupObjectId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            GroupIdentifierRequiredMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetGroupMemberWhitespaceGroupDisplayName_ThrowsInvalidInput()
    {
        string whitespaceGroupDisplayName = " ";
        using PowerShell shell = CreateShell<GetAzMsGraphGroupMemberCmdlet>();
        shell.AddParameter("GroupDisplayName", whitespaceGroupDisplayName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            GroupIdentifierRequiredMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // GroupObjectId and GroupDisplayName are in different parameter sets, so PowerShell rejects both together before
    // the extension's "not both" check.
    [Fact]
    public void Invoke_GetGroupMemberGroupObjectIdAndGroupDisplayName_ThrowsParameterBindingException()
    {
        string groupDisplayName = "Sales";
        using PowerShell shell = CreateShell<GetAzMsGraphGroupMemberCmdlet>();
        shell.AddParameter("GroupObjectId", GroupId).AddParameter("GroupDisplayName", groupDisplayName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Parameter set cannot be resolved using the specified named parameters. One or more parameters issued cannot be used together or an insufficient number of parameters were provided.",
            Assert.IsType<ParameterBindingException>(exception).Message);
    }
}
