// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests.Commands.Resources;

/// <summary>
/// Runs subscription deployment cmdlets in PowerShell through the real engine and checks the error for each rejected
/// input. The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class SubDeploymentCommandTests : IDisposable
{
    private const string TemplateSourceMessage = "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.";
    private const string InvalidIdMessage = "Invalid deployment id '/subscriptions/sub'. Expected /subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public SubDeploymentCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── New-AzSubDeployment ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_NewSubDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string location = "westus";
        using PowerShell shell = CreateShell<NewSubDeploymentCmdlet>();
        shell.AddParameter("Name", "dep").AddParameter("Location", location).AddParameter("Mode", "Incremental");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateSet] rejects the mode while binding (Test-AzSubDeployment has the same set). The binder throws the
    // internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_NewSubDeploymentModeNotIncrementalOrComplete_ThrowsParameterBindingException()
    {
        string modeNotIncrementalOrComplete = "Replace";
        using PowerShell shell = CreateShell<NewSubDeploymentCmdlet>();
        shell.AddParameter("Name", "dep")
            .AddParameter("Location", "westus")
            .AddParameter("Mode", modeNotIncrementalOrComplete);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'Mode'. The argument \"Replace\" does not belong to the set \"Incremental,Complete\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // ── Test-AzSubDeployment ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_TestSubDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string location = "westus";
        using PowerShell shell = CreateShell<TestSubDeploymentCmdlet>();
        shell.AddParameter("Name", "dep").AddParameter("Location", location).AddParameter("Mode", "Incremental");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzSubDeploymentWhatIfResult ────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_GetSubDeploymentWhatIfResultWithoutTemplate_ThrowsArgumentException()
    {
        string location = "westus";
        using PowerShell shell = CreateShell<GetSubDeploymentWhatIfResultCmdlet>();
        shell.AddParameter("Location", location);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateSet] rejects the format while binding (Get-AzTenantDeploymentWhatIfResult has the same set). The binder
    // throws the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_GetSubDeploymentWhatIfResultResultFormatNotInSet_ThrowsParameterBindingException()
    {
        string resultFormatNotInSet = "Full";
        using PowerShell shell = CreateShell<GetSubDeploymentWhatIfResultCmdlet>();
        shell.AddParameter("Location", "westus").AddParameter("ResultFormat", resultFormatNotInSet);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'ResultFormat'. The argument \"Full\" does not belong to the set \"FullResourcePayloads,ResourceIdOnly\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // ── deployment objects ─────────────────────────────────────────────────────────────────────
    // These cmdlets check the piped deployment themselves and write a non-terminating error without sending a request;
    // -ErrorAction Stop turns that error into the exception checked here.

    [Fact]
    public void Invoke_GetSubDeploymentInputObjectInvalidId_ThrowsPSArgumentException()
    {
        DeploymentExtended deploymentWithInvalidId = new() { Id = "/subscriptions/sub" };
        using PowerShell shell = CreateShell<GetSubDeploymentCmdlet>();
        shell.AddParameter("InputObject", deploymentWithInvalidId).AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }

    [Fact]
    public void Invoke_StopSubDeploymentInputObjectInvalidId_ThrowsPSArgumentException()
    {
        DeploymentExtended deploymentWithInvalidId = new() { Id = "/subscriptions/sub" };
        using PowerShell shell = CreateShell<StopSubDeploymentCmdlet>();
        shell.AddParameter("InputObject", deploymentWithInvalidId)
            .AddParameter("Confirm", false)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }

    [Fact]
    public void Invoke_RemoveSubDeploymentInputObjectInvalidId_ThrowsPSArgumentException()
    {
        DeploymentExtended deploymentWithInvalidId = new() { Id = "/subscriptions/sub" };
        using PowerShell shell = CreateShell<RemoveSubDeploymentCmdlet>();
        shell.AddParameter("InputObject", deploymentWithInvalidId)
            .AddParameter("Confirm", false)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }

    [Fact]
    public void Invoke_SaveSubDeploymentTemplateInputObjectInvalidId_ThrowsPSArgumentException()
    {
        DeploymentExtended deploymentWithInvalidId = new() { Id = "/subscriptions/sub" };
        using PowerShell shell = CreateShell<SaveSubDeploymentTemplateCmdlet>();
        shell.AddParameter("InputObject", deploymentWithInvalidId)
            .AddParameter("Confirm", false)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }

    [Fact]
    public void Invoke_GetDeploymentOperationDeploymentObjectWithoutName_ThrowsPSArgumentException()
    {
        PSObject deploymentWithoutName = new();
        using PowerShell shell = CreateShell<GetSubscriptionDeploymentOperationCmdlet>();
        shell.AddParameter("DeploymentObject", deploymentWithoutName)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The deployment object has no DeploymentName or Name property.",
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }
}
