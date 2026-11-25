// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;

namespace Cmdlets.Tests.Commands.ManagedIdentity;

/// <summary>
/// Runs user-assigned identity cmdlets in PowerShell through the real engine and checks the error for each rejected
/// input. The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class IdentityCommandTests : IDisposable
{
    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public IdentityCommandTests()
    {
        _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── New-AzUserAssignedIdentity ─────────────────────────────────────────────────────────────

    // [ValidateSet] rejects the scope while binding (Update-AzUserAssignedIdentity has the same set). The binder throws
    // the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_NewUserAssignedIdentityIsolationScopeNotNoneOrRegional_ThrowsParameterBindingException()
    {
        string isolationScopeNotNoneOrRegional = "Global";
        using PowerShell shell = CreateShell<NewAzUserAssignedIdentityCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "id")
            .AddParameter("Location", "westus")
            .AddParameter("IsolationScope", isolationScopeNotNoneOrRegional);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'IsolationScope'. The argument \"Global\" does not belong to the set \"None,Regional\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }
}
