// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests.Commands.Resources;

/// <summary>
/// Runs resource group cmdlets in PowerShell through the real engine and checks the error for each rejected input.
/// The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class GroupCommandTests : IDisposable
{
    private const string NameOrIdMessage = "Specify exactly one of resource group name or resource group id.";
    private const string InvalidIdMessage = "Invalid resource group id '/subscriptions/sub'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public GroupCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── Remove-AzResourceGroup ─────────────────────────────────────────────────────────────────
    // [ValidateNotNullOrEmpty] rejects an empty Name or Id while binding, so only whitespace reaches the extension.

    [Fact]
    public void Invoke_RemoveResourceGroupWhitespaceName_ThrowsInvalidInput()
    {
        string whitespaceName = " ";
        using PowerShell shell = CreateShell<RemoveResourceGroupCmdlet>();
        shell.AddParameter("Name", whitespaceName).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NameOrIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Current behaviour: the message asks for exactly one of name or id although only an id was given.
    [Fact]
    public void Invoke_RemoveResourceGroupWhitespaceId_ThrowsInvalidInput()
    {
        string whitespaceId = " ";
        using PowerShell shell = CreateShell<RemoveResourceGroupCmdlet>();
        shell.AddParameter("Id", whitespaceId).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NameOrIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveResourceGroupIdWithoutResourceGroup_ThrowsInvalidInput()
    {
        string idWithoutResourceGroup = "/subscriptions/sub";
        using PowerShell shell = CreateShell<RemoveResourceGroupCmdlet>();
        shell.AddParameter("Id", idWithoutResourceGroup).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Set-AzResourceGroup ────────────────────────────────────────────────────────────────────

    // The cmdlet itself rejects the key while converting the hashtable, before the engine runs.
    [Fact]
    public void Invoke_SetResourceGroupWhitespaceTagKey_ThrowsInvalidInput()
    {
        Hashtable tagWithWhitespaceKey = new() { [" "] = "value" };
        using PowerShell shell = CreateShell<SetResourceGroupCmdlet>();
        shell.AddParameter("Name", "rg").AddParameter("Tag", tagWithWhitespaceKey);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Tag keys cannot be empty.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_SetResourceGroupWhitespaceName_ThrowsInvalidInput()
    {
        string whitespaceName = " ";
        using PowerShell shell = CreateShell<SetResourceGroupCmdlet>();
        shell.AddParameter("Name", whitespaceName).AddParameter("Tag", new Hashtable { ["env"] = "test" });

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NameOrIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Current behaviour: the message asks for exactly one of name or id although only an id was given.
    [Fact]
    public void Invoke_SetResourceGroupWhitespaceId_ThrowsInvalidInput()
    {
        string whitespaceId = " ";
        using PowerShell shell = CreateShell<SetResourceGroupCmdlet>();
        shell.AddParameter("Id", whitespaceId).AddParameter("Tag", new Hashtable { ["env"] = "test" });

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NameOrIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_SetResourceGroupIdWithoutResourceGroup_ThrowsInvalidInput()
    {
        string idWithoutResourceGroup = "/subscriptions/sub";
        using PowerShell shell = CreateShell<SetResourceGroupCmdlet>();
        shell.AddParameter("Id", idWithoutResourceGroup).AddParameter("Tag", new Hashtable { ["env"] = "test" });

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
