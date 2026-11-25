// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;

namespace Cmdlets.Tests.Commands.ManagedIdentity;

/// <summary>
/// Runs federated identity credential cmdlets in PowerShell through the real engine and checks the error for each
/// rejected input. The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class FederatedCredentialCommandTests : IDisposable
{
    private const string ClaimsMatchingExpressionMessage = "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public FederatedCredentialCommandTests()
    {
        _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── claims matching expression (New- and Update-AzFederatedIdentityCredential) ─────────────

    [Fact]
    public void Invoke_NewFederatedIdentityCredentialClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput()
    {
        int claimsMatchingExpressionVersion = 1;
        using PowerShell shell = CreateShell<NewAzFederatedIdentityCredentialCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("IdentityName", "id")
            .AddParameter("Name", "fc")
            .AddParameter("ClaimsMatchingExpressionVersion", claimsMatchingExpressionVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ClaimsMatchingExpressionMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateFederatedIdentityCredentialClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput()
    {
        int claimsMatchingExpressionVersion = 1;
        using PowerShell shell = CreateShell<UpdateAzFederatedIdentityCredentialCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("IdentityName", "id")
            .AddParameter("Name", "fc")
            .AddParameter("ClaimsMatchingExpressionVersion", claimsMatchingExpressionVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ClaimsMatchingExpressionMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewFederatedIdentityCredentialClaimsMatchingExpressionValueWithoutVersion_ThrowsInvalidInput()
    {
        string claimsMatchingExpressionValue = "claims['sub'] eq 'repo'";
        using PowerShell shell = CreateShell<NewAzFederatedIdentityCredentialCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("IdentityName", "id")
            .AddParameter("Name", "fc")
            .AddParameter("ClaimsMatchingExpressionValue", claimsMatchingExpressionValue);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ClaimsMatchingExpressionMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewFederatedIdentityCredentialBlankClaimsMatchingExpressionValue_ThrowsInvalidInput()
    {
        string blankClaimsMatchingExpressionValue = " ";
        using PowerShell shell = CreateShell<NewAzFederatedIdentityCredentialCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("IdentityName", "id")
            .AddParameter("Name", "fc")
            .AddParameter("ClaimsMatchingExpressionVersion", 1)
            .AddParameter("ClaimsMatchingExpressionValue", blankClaimsMatchingExpressionValue);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ClaimsMatchingExpressionMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
