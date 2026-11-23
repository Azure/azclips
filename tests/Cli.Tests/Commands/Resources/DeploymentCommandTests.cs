// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs deployment CLI commands through the real engine and checks the error for each rejected input. The wire mock
/// is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class DeploymentCommandTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string TemplateUri = "https://example.com/template.json";
    private const string RollbackOptionsMessage = "Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive.";
    private const string TemplateSourceMessage = "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public DeploymentCommandTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── deployment template and parameters (shared by the create, validate and what-if commands) ─
    // Current behaviour: these messages name the PowerShell parameters (TemplateUri, ParametersFile, ...) and
    // TemplateObject, which CLI users cannot pass, and some rules throw ArgumentException where others throw
    // InvalidInputException.

    [Theory]
    [InlineData("deployment", "group", "create", "--resource-group", "rg")]
    [InlineData("deployment", "group", "what-if", "--resource-group", "rg")]
    [InlineData("deployment", "group", "validate", "--resource-group", "rg")]
    [InlineData("deployment", "sub", "create", "--location", "westus")]
    [InlineData("deployment", "sub", "what-if", "--location", "westus")]
    [InlineData("deployment", "sub", "validate", "--location", "westus")]
    [InlineData("deployment", "mg", "create", "--management-group-id", "mg", "--location", "westus")]
    [InlineData("deployment", "mg", "what-if", "--management-group-id", "mg", "--location", "westus")]
    [InlineData("deployment", "mg", "validate", "--management-group-id", "mg", "--location", "westus")]
    [InlineData("deployment", "tenant", "create", "--location", "westus")]
    [InlineData("deployment", "tenant", "what-if", "--location", "westus")]
    [InlineData("deployment", "tenant", "validate", "--location", "westus")]
    public async Task RunAsync_DeploymentWithoutTemplate_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(TemplateSourceMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentTwoTemplateSources_ThrowsArgumentException()
    {
        string templateSpecId = "/subscriptions/sub/templateSpecs/spec/versions/1";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", "rg",
                "--template-uri", TemplateUri,
                "--template-spec-id", templateSpecId,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(TemplateSourceMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentParametersUriAndParametersFile_ThrowsInvalidInput()
    {
        string parametersUri = "https://example.com/parameters.json";
        string parametersFile = "parameters.json";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", "rg",
                "--template-uri", TemplateUri,
                "--parameters-uri", parametersUri,
                "--parameters-file", parametersFile,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Provide at most one of ParametersUri or ParametersFile.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInlineParameterWithParametersUri_ThrowsInvalidInput()
    {
        string parametersUri = "https://example.com/parameters.json";
        string inlineParameter = "size=1";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", "rg",
                "--template-uri", TemplateUri,
                "--parameters-uri", parametersUri,
                "--parameter", inlineParameter,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. " +
            "Provide a parameters file instead, or remove the parameters URI.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentBlankInlineParameter_ThrowsInvalidInput()
    {
        string blankInlineParameter = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", "rg", "--template-uri", TemplateUri, "--parameter", blankInlineParameter]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid inline parameter: the token is empty; expected KEY=VALUE.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInlineParameterWithoutKey_ThrowsInvalidInput()
    {
        string inlineParameterWithoutKey = "=1";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", "rg", "--template-uri", TemplateUri, "--parameter", inlineParameterWithoutKey]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid inline parameter '=1': expected KEY=VALUE with a non-empty key.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInlineParameterWithWhitespaceKey_ThrowsInvalidInput()
    {
        string inlineParameterWithWhitespaceKey = " =1";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", "rg",
                "--template-uri", TemplateUri,
                "--parameter", inlineParameterWithWhitespaceKey,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid inline parameter ' =1': the key is empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentQueryStringWithoutTemplateUri_ThrowsArgumentException()
    {
        string queryString = "sv=1";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", "rg",
                "--template-spec-id", "/subscriptions/sub/templateSpecs/spec/versions/1",
                "--query-string", queryString,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "QueryString requires TemplateUri; it is only carried on the template link built from a template URI.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInvalidName_ThrowsInvalidInput()
    {
        string invalidDeploymentName = "bad name!";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", "rg", "--template-uri", TemplateUri, "--name", invalidDeploymentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentMissingTemplateFile_ThrowsFileNotFound()
    {
        string templateFile = Path.Combine(_missingDirectory, "missing.json");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", "rg", "--template-file", templateFile]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"File not found: {templateFile}", Assert.IsType<FileNotFoundException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentMissingParametersFile_ThrowsFileNotFound()
    {
        string parametersFile = Path.Combine(_missingDirectory, "missing.parameters.json");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "group", "create", "--resource-group", "rg", "--template-uri", TemplateUri, "--parameters-file", parametersFile]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"File not found: {parametersFile}", Assert.IsType<FileNotFoundException>(exception).Message);
    }

    // ── deployment group create, what-if and validate ──────────────────────────────────────────
    // Current behaviour: these messages name the PowerShell parameters, and validate throws ArgumentException for
    // the rollback rule where create throws InvalidInputException.

    [Theory]
    [InlineData("deployment", "group", "create")]
    [InlineData("deployment", "group", "what-if")]
    [InlineData("deployment", "group", "validate")]
    public async Task RunAsync_DeploymentGroupBlankResourceGroup_ThrowsInvalidInput(params string[] commandLine)
    {
        string blankResourceGroup = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [.. commandLine, "--resource-group", blankResourceGroup, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("ResourceGroupName must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateBothRollbackOptions_ThrowsInvalidInput()
    {
        string rollbackToDeploymentName = "previous";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "create",
                "--resource-group", "rg",
                "--template-uri", TemplateUri,
                "--rollback-on-error",
                "--rollback-to-deployment-name", rollbackToDeploymentName,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(RollbackOptionsMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupValidateBothRollbackOptions_ThrowsArgumentException()
    {
        string rollbackToDeploymentName = "previous";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "deployment", "group", "validate",
                "--resource-group", "rg",
                "--template-uri", TemplateUri,
                "--rollback-on-error",
                "--rollback-to-deployment-name", rollbackToDeploymentName,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(RollbackOptionsMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── deployment mg create, what-if and validate ─────────────────────────────────────────────
    // Current behaviour: these messages name the PowerShell parameters.

    [Theory]
    [InlineData("deployment", "mg", "create")]
    [InlineData("deployment", "mg", "what-if")]
    [InlineData("deployment", "mg", "validate")]
    public async Task RunAsync_DeploymentMgBlankManagementGroupId_ThrowsInvalidInput(params string[] commandLine)
    {
        string blankManagementGroupId = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [.. commandLine, "--management-group-id", blankManagementGroupId, "--location", "westus", "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("ManagementGroupId must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("deployment", "mg", "create")]
    [InlineData("deployment", "mg", "what-if")]
    [InlineData("deployment", "mg", "validate")]
    public async Task RunAsync_DeploymentMgBlankLocation_ThrowsInvalidInput(params string[] commandLine)
    {
        string blankLocation = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [.. commandLine, "--management-group-id", "mg", "--location", blankLocation, "--template-uri", TemplateUri]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Location must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── deployment tenant wait ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitWithoutCondition_ThrowsInvalidInput()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["deployment", "tenant", "wait", "--name", "deployment"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitInvalidCustomExpression_ThrowsInvalidInput()
    {
        string invalidJmesPathExpression = "[[";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "tenant", "wait", "--name", "deployment", "--custom", invalidJmesPathExpression]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "--custom expression '[[' is not valid JMESPath: Error(1, 1): syntax, near ''.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // A zero timeout ends the wait before the first poll.
    [Fact]
    public async Task RunAsync_DeploymentTenantWaitZeroTimeout_ThrowsInvalidOperationException()
    {
        string zeroTimeout = "0";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["deployment", "tenant", "wait", "--name", "deployment", "--created", "--timeout", zeroTimeout]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Wait operation timed-out after 0 seconds", Assert.IsType<InvalidOperationException>(exception).Message);
    }
}
