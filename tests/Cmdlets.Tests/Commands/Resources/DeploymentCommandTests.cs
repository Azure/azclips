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
/// Runs resource group deployment cmdlets in PowerShell through the real engine and checks the error for each rejected
/// input. The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class DeploymentCommandTests : IDisposable
{
    private const string TemplateUri = "https://example.com/template.json";
    private const string TemplateSourceMessage = "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.";
    private const string ResourceGroupNameMessage = "ResourceGroupName must be provided.";
    private const string ModeMessage = "Mode must be either 'Incremental' or 'Complete'.";
    private const string RollbackOptionsMessage = "Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive.";
    private const string InvalidIdMessage = "Invalid deployment id '/subscriptions/sub'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Resources/deployments/{deploymentName}.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public DeploymentCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── New-AzResourceGroupDeployment ──────────────────────────────────────────────────────────
    // The template and parameter checks are shared by every deployment cmdlet, so apart from the missing template they
    // run once, here. [Parameter(Mandatory)] rejects an empty ResourceGroupName while binding, so only whitespace
    // reaches the extension.

    [Fact]
    public void Invoke_NewResourceGroupDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", resourceGroupName).AddParameter("Name", "dep");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentTwoTemplateSources_ThrowsArgumentException()
    {
        string templateSpecId = "/subscriptions/sub/templateSpecs/spec/versions/1";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("TemplateSpecId", templateSpecId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentParametersUriAndParametersFile_ThrowsInvalidInput()
    {
        string parametersFile = Path.Combine(_missingDirectory, "parameters.json");
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("ParametersUri", "https://example.com/parameters.json")
            .AddParameter("ParametersFile", parametersFile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Provide at most one of ParametersUri or ParametersFile.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentInlineParameterWithParametersUri_ThrowsInvalidInput()
    {
        string[] inlineParameter = ["size=1"];
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("ParametersUri", "https://example.com/parameters.json")
            .AddParameter("Parameter", inlineParameter);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. " +
            "Provide a parameters file instead, or remove the parameters URI.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentBlankInlineParameter_ThrowsInvalidInput()
    {
        string[] blankInlineParameter = [""];
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Parameter", blankInlineParameter);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid inline parameter: the token is empty; expected KEY=VALUE.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentInlineParameterWithoutKey_ThrowsInvalidInput()
    {
        string[] inlineParameterWithoutKey = ["=1"];
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Parameter", inlineParameterWithoutKey);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid inline parameter '=1': expected KEY=VALUE with a non-empty key.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentInlineParameterWithWhitespaceKey_ThrowsInvalidInput()
    {
        string[] inlineParameterWithWhitespaceKey = [" =1"];
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Parameter", inlineParameterWithWhitespaceKey);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid inline parameter ' =1': the key is empty.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentQueryStringWithoutTemplateUri_ThrowsArgumentException()
    {
        string queryString = "sv=1";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateSpecId", "/subscriptions/sub/templateSpecs/spec/versions/1")
            .AddParameter("QueryString", queryString);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "QueryString requires TemplateUri; it is only carried on the template link built from a template URI.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentInvalidName_ThrowsInvalidInput()
    {
        string invalidDeploymentName = "bad name!";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", invalidDeploymentName)
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentMissingTemplateFile_ThrowsFileNotFound()
    {
        string missingTemplateFile = Path.Combine(_missingDirectory, "missing.json");
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateFile", missingTemplateFile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"File not found: {missingTemplateFile}",
            Assert.IsType<FileNotFoundException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentMissingParametersFile_ThrowsFileNotFound()
    {
        string missingParametersFile = Path.Combine(_missingDirectory, "missing.parameters.json");
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("ParametersFile", missingParametersFile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"File not found: {missingParametersFile}",
            Assert.IsType<FileNotFoundException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentWhitespaceResourceGroupName_ThrowsInvalidInput()
    {
        string whitespaceResourceGroupName = " ";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", whitespaceResourceGroupName)
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ResourceGroupNameMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentBothRollbackOptions_ThrowsInvalidInput()
    {
        string rollbackToDeploymentName = "previous";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("RollbackOnError", true)
            .AddParameter("RollbackToDeploymentName", rollbackToDeploymentName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            RollbackOptionsMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Mode has no [ValidateSet] on this cmdlet, so the extension rejects it.
    [Fact]
    public void Invoke_NewResourceGroupDeploymentModeNotIncrementalOrComplete_ThrowsInvalidInput()
    {
        string modeNotIncrementalOrComplete = "Bogus";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Mode", modeNotIncrementalOrComplete);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ModeMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Test-AzResourceGroupDeployment ─────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_TestResourceGroupDeploymentWithoutTemplate_ThrowsArgumentException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<TestResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", resourceGroupName).AddParameter("Name", "dep");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_TestResourceGroupDeploymentWhitespaceResourceGroupName_ThrowsInvalidInput()
    {
        string whitespaceResourceGroupName = " ";
        using PowerShell shell = CreateShell<TestResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", whitespaceResourceGroupName)
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ResourceGroupNameMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Current behaviour: validate throws ArgumentException where New-AzResourceGroupDeployment throws InvalidInput.
    [Fact]
    public void Invoke_TestResourceGroupDeploymentBothRollbackOptions_ThrowsArgumentException()
    {
        string rollbackToDeploymentName = "previous";
        using PowerShell shell = CreateShell<TestResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("RollbackOnError", true)
            .AddParameter("RollbackToDeploymentName", rollbackToDeploymentName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            RollbackOptionsMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_TestResourceGroupDeploymentModeNotIncrementalOrComplete_ThrowsInvalidInput()
    {
        string modeNotIncrementalOrComplete = "Bogus";
        using PowerShell shell = CreateShell<TestResourceGroupDeploymentCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Mode", modeNotIncrementalOrComplete);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ModeMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── New-AzResourceGroupDeploymentWhatIf ────────────────────────────────────────────────────

    [Fact]
    public void Invoke_NewResourceGroupDeploymentWhatIfWithoutTemplate_ThrowsArgumentException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentWhatIfCmdlet>();
        shell.AddParameter("ResourceGroupName", resourceGroupName).AddParameter("Name", "dep");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            TemplateSourceMessage,
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentWhatIfWhitespaceResourceGroupName_ThrowsInvalidInput()
    {
        string whitespaceResourceGroupName = " ";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentWhatIfCmdlet>();
        shell.AddParameter("ResourceGroupName", whitespaceResourceGroupName)
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ResourceGroupNameMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewResourceGroupDeploymentWhatIfModeNotIncrementalOrComplete_ThrowsInvalidInput()
    {
        string modeNotIncrementalOrComplete = "Bogus";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentWhatIfCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("Mode", modeNotIncrementalOrComplete);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ModeMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ResultFormat has no [ValidateSet] on this cmdlet, unlike the subscription and tenant what-if cmdlets, so the
    // extension rejects it.
    [Fact]
    public void Invoke_NewResourceGroupDeploymentWhatIfResultFormatNotInList_ThrowsInvalidInput()
    {
        string resultFormatNotInList = "Bogus";
        using PowerShell shell = CreateShell<NewResourceGroupDeploymentWhatIfCmdlet>();
        shell.AddParameter("ResourceGroupName", "rg")
            .AddParameter("Name", "dep")
            .AddParameter("TemplateUri", TemplateUri)
            .AddParameter("ResultFormat", resultFormatNotInList);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzResourceGroupDeployment ──────────────────────────────────────────────────────────

    // Current behaviour: a malformed id throws InvalidOperationException rather than InvalidInput.
    [Fact]
    public void Invoke_GetResourceGroupDeploymentInvalidId_ThrowsInvalidOperationException()
    {
        string idWithoutDeployment = "/subscriptions/sub";
        using PowerShell shell = CreateShell<GetResourceGroupDeploymentCmdlet>();
        shell.AddParameter("Id", idWithoutDeployment);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidOperationException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Remove-AzResourceGroupDeployment ───────────────────────────────────────────────────────

    // Current behaviour: a malformed id throws InvalidOperationException rather than InvalidInput.
    [Fact]
    public void Invoke_RemoveResourceGroupDeploymentInvalidId_ThrowsInvalidOperationException()
    {
        string idWithoutDeployment = "/subscriptions/sub";
        using PowerShell shell = CreateShell<RemoveResourceGroupDeploymentCmdlet>();
        shell.AddParameter("Id", idWithoutDeployment).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            InvalidIdMessage,
            Assert.IsType<InvalidOperationException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
