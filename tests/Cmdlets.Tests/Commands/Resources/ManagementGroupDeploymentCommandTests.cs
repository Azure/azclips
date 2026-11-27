// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
/// Runs management group deployment cmdlets in PowerShell through the real engine and checks the error for each
/// rejected input. The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class ManagementGroupDeploymentCommandTests : IDisposable
{
    private const string TemplateUri = "https://example.com/template.json";
    private const string TemplateSourceMessage = "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.";
    private const string ManagementGroupIdMessage = "ManagementGroupId must be provided.";
    private const string LocationMessage = "Location must be provided.";
    private const string ModeMessage = "Mode must be either 'Incremental' or 'Complete'.";
    private const string InvalidIdMessage = "Invalid deployment id '/subscriptions/sub'. Expected /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public ManagementGroupDeploymentCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── New-AzManagementGroupDeployment ────────────────────────────────────────────────────────
    // [Parameter(Mandatory)] rejects an empty ManagementGroupId or Location while binding, so only whitespace reaches
    // the extension.

    [Fact]
    public void Invoke_NewManagementGroupDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string managementGroupId = "mg";
        using PowerShell shell = CreateShell<NewManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", managementGroupId)
            .AddParameter("Location", "westus")
            .AddParameter("Name", "dep");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewManagementGroupDeploymentWhitespaceManagementGroupId_ThrowsInvalidInput()
    {
        string whitespaceManagementGroupId = " ";
        using PowerShell shell = CreateShell<NewManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", whitespaceManagementGroupId)
            .AddParameter("Location", "westus")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ManagementGroupIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewManagementGroupDeploymentWhitespaceLocation_ThrowsInvalidInput()
    {
        string whitespaceLocation = " ";
        using PowerShell shell = CreateShell<NewManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", "mg")
            .AddParameter("Location", whitespaceLocation)
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            LocationMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Mode has no [ValidateSet] on this cmdlet, so the extension rejects it.
    [Fact]
    public void Invoke_NewManagementGroupDeploymentModeNotIncrementalOrComplete_ThrowsInvalidInput()
    {
        string modeNotIncrementalOrComplete = "Bogus";
        using PowerShell shell = CreateShell<NewManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", "mg")
            .AddParameter("Location", "westus")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Mode", modeNotIncrementalOrComplete);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ModeMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Test-AzManagementGroupDeployment ───────────────────────────────────────────────────────

    [Fact]
    public void Invoke_TestManagementGroupDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string managementGroupId = "mg";
        using PowerShell shell = CreateShell<TestManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", managementGroupId).AddParameter("Location", "westus");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_TestManagementGroupDeploymentWhitespaceManagementGroupId_ThrowsInvalidInput()
    {
        string whitespaceManagementGroupId = " ";
        using PowerShell shell = CreateShell<TestManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", whitespaceManagementGroupId)
            .AddParameter("Location", "westus")
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ManagementGroupIdMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_TestManagementGroupDeploymentWhitespaceLocation_ThrowsInvalidInput()
    {
        string whitespaceLocation = " ";
        using PowerShell shell = CreateShell<TestManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", "mg")
            .AddParameter("Location", whitespaceLocation)
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            LocationMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Current behaviour: the mode is checked although validation always uses Incremental.
    [Fact]
    public void Invoke_TestManagementGroupDeploymentModeNotIncrementalOrComplete_ThrowsInvalidInput()
    {
        string modeNotIncrementalOrComplete = "Bogus";
        using PowerShell shell = CreateShell<TestManagementGroupDeploymentCmdlet>();
        shell.AddParameter("ManagementGroupId", "mg")
            .AddParameter("Location", "westus")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Mode", modeNotIncrementalOrComplete);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ModeMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-, Stop-, Remove-AzManagementGroupDeployment ────────────────────────────────────────
    // Current behaviour: a malformed id throws InvalidOperationException rather than InvalidInput.

    [Fact]
    public void Invoke_GetManagementGroupDeploymentInvalidId_ThrowsInvalidOperationException()
    {
        string idWithoutDeployment = "/subscriptions/sub";
        using PowerShell shell = CreateShell<GetManagementGroupDeploymentCmdlet>();
        shell.AddParameter("Id", idWithoutDeployment);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidOperationException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_StopManagementGroupDeploymentInvalidId_ThrowsInvalidOperationException()
    {
        string idWithoutDeployment = "/subscriptions/sub";
        using PowerShell shell = CreateShell<StopManagementGroupDeploymentCmdlet>();
        shell.AddParameter("Id", idWithoutDeployment).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidOperationException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveManagementGroupDeploymentInvalidId_ThrowsInvalidOperationException()
    {
        string idWithoutDeployment = "/subscriptions/sub";
        using PowerShell shell = CreateShell<RemoveManagementGroupDeploymentCmdlet>();
        shell.AddParameter("Id", idWithoutDeployment).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidOperationException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzManagementGroupDeploymentOperation ───────────────────────────────────────────────
    // The cmdlet checks the piped deployment itself and writes a non-terminating error without sending a request;
    // -ErrorAction Stop turns that error into the exception checked here.

    [Fact]
    public void Invoke_GetManagementGroupDeploymentOperationDeploymentObjectWithoutName_ThrowsPSArgumentException()
    {
        PSObject deploymentWithoutName = new();
        using PowerShell shell = CreateShell<GetManagementGroupDeploymentOperationCmdlet>();
        shell.AddParameter("DeploymentObject", deploymentWithoutName)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The deployment object has no DeploymentName or Name property.",
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }

    [Fact]
    public void Invoke_GetManagementGroupDeploymentOperationDeploymentObjectWithoutManagementGroup_ThrowsPSArgumentException()
    {
        PSObject deploymentWithoutManagementGroup = new();
        deploymentWithoutManagementGroup.Properties.Add(new PSNoteProperty("Name", "dep"));
        deploymentWithoutManagementGroup.Properties.Add(new PSNoteProperty("Id", "/subscriptions/sub/providers/Microsoft.Resources/deployments/dep"));
        using PowerShell shell = CreateShell<GetManagementGroupDeploymentOperationCmdlet>();
        shell.AddParameter("DeploymentObject", deploymentWithoutManagementGroup)
            .AddParameter("ErrorAction", ActionPreference.Stop);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The deployment object has no ManagementGroupId property, and its Id is not a management group deployment id.",
            Assert.IsType<PSArgumentException>(Assert.IsType<ActionPreferenceStopException>(exception).ErrorRecord.Exception).Message);
    }
}
