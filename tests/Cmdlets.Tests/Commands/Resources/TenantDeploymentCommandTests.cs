// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests.Commands.Resources;

/// <summary>
/// Runs tenant deployment cmdlets in PowerShell through the real engine and checks the error for each rejected input.
/// The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class TenantDeploymentCommandTests : IDisposable
{
    private const string TemplateSourceMessage = "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public TenantDeploymentCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── New-AzTenantDeployment ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_NewTenantDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string location = "westus";
        using PowerShell shell = CreateShell<NewTenantDeploymentCmdlet>();
        shell.AddParameter("Location", location);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Test-AzTenantDeployment ────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_TestTenantDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string location = "westus";
        using PowerShell shell = CreateShell<TestTenantDeploymentCmdlet>();
        shell.AddParameter("Location", location);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzTenantDeploymentWhatIfResult ─────────────────────────────────────────────────────

    [Fact]
    public void Invoke_GetTenantDeploymentWhatIfResultWithoutTemplate_ThrowsArgumentException()
    {
        string location = "westus";
        using PowerShell shell = CreateShell<GetTenantDeploymentWhatIfResultCmdlet>();
        shell.AddParameter("Location", location);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzTenantDeploymentOperation ────────────────────────────────────────────────────────
    // The cmdlet checks the piped deployment itself and writes a non-terminating error without sending a request;
    // -ErrorAction Stop turns that error into the exception checked here.

    [Fact]
    public void Invoke_GetTenantDeploymentOperationDeploymentObjectWithoutName_ThrowsPSArgumentException()
    {
        PSObject deploymentWithoutName = new();
        using PowerShell shell = CreateShell<GetTenantDeploymentOperationCmdlet>();
        shell.AddParameter("DeploymentObject", deploymentWithoutName)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The deployment object has no DeploymentName or Name property.",
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }
}
