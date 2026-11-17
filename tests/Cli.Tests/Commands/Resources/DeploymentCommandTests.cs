// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
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

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public DeploymentCommandTests() => _engine = CommandTestHelpers.CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── deployment template and parameters (shared by the create, validate and what-if commands) ─
    // Current behaviour: these messages name the PowerShell parameters (TemplateUri, ParametersFile, ...) and
    // TemplateObject, which CLI users cannot pass, and some rules throw ArgumentException where others throw
    // InvalidInputException.

    [Theory]
    [InlineData("deployment group create", new[] { "--resource-group", "rg" })]
    [InlineData("deployment group what-if", new[] { "--resource-group", "rg" })]
    [InlineData("deployment group validate", new[] { "--resource-group", "rg" })]
    [InlineData("deployment sub create", new[] { "--location", "westus" })]
    [InlineData("deployment sub what-if", new[] { "--location", "westus" })]
    [InlineData("deployment sub validate", new[] { "--location", "westus" })]
    [InlineData("deployment mg create", new[] { "--management-group-id", "mg", "--location", "westus" })]
    [InlineData("deployment mg what-if", new[] { "--management-group-id", "mg", "--location", "westus" })]
    [InlineData("deployment mg validate", new[] { "--management-group-id", "mg", "--location", "westus" })]
    [InlineData("deployment tenant create", new[] { "--location", "westus" })]
    [InlineData("deployment tenant what-if", new[] { "--location", "westus" })]
    [InlineData("deployment tenant validate", new[] { "--location", "westus" })]
    public async Task RunAsync_DeploymentWithoutTemplate_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentTwoTemplateSources_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "deployment group create",
            ["--resource-group", "rg", "--template-uri", TemplateUri, "--template-spec-id", "/subscriptions/sub/templateSpecs/spec/versions/1"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentParametersUriAndParametersFile_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "deployment group create",
            ["--resource-group", "rg", "--template-uri", TemplateUri, "--parameters-uri", "https://example.com/parameters.json", "--parameters-file", "parameters.json"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Provide at most one of ParametersUri or ParametersFile.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInlineParameterWithParametersUri_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "deployment group create",
            ["--resource-group", "rg", "--template-uri", TemplateUri, "--parameters-uri", "https://example.com/parameters.json", "--parameter", "size=1"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. " +
            "Provide a parameters file instead, or remove the parameters URI.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentBlankInlineParameter_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment group create", ["--resource-group", "rg", "--template-uri", TemplateUri, "--parameter", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid inline parameter: the token is empty; expected KEY=VALUE.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInlineParameterWithoutKey_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment group create", ["--resource-group", "rg", "--template-uri", TemplateUri, "--parameter", "=1"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid inline parameter '=1': expected KEY=VALUE with a non-empty key.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInlineParameterWithWhitespaceKey_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment group create", ["--resource-group", "rg", "--template-uri", TemplateUri, "--parameter", " =1"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid inline parameter ' =1': the key is empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentQueryStringWithoutTemplateUri_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "deployment group create",
            ["--resource-group", "rg", "--template-spec-id", "/subscriptions/sub/templateSpecs/spec/versions/1", "--query-string", "sv=1"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "QueryString requires TemplateUri; it is only carried on the template link built from a template URI.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentInvalidName_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment group create", ["--resource-group", "rg", "--template-uri", TemplateUri, "--name", "bad name!"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentMissingTemplateFile_ThrowsFileNotFound()
    {
        string templateFile = Path.Combine(_missingDirectory, "missing.json");
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment group create", ["--resource-group", "rg", "--template-file", templateFile], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"File not found: {templateFile}", Assert.IsType<FileNotFoundException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentMissingParametersFile_ThrowsFileNotFound()
    {
        string parametersFile = Path.Combine(_missingDirectory, "missing.parameters.json");
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment group create", ["--resource-group", "rg", "--template-uri", TemplateUri, "--parameters-file", parametersFile], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"File not found: {parametersFile}", Assert.IsType<FileNotFoundException>(exception).Message);
    }

    // ── deployment group create, what-if and validate ──────────────────────────────────────────
    // Current behaviour: these messages name the PowerShell parameters, and validate throws ArgumentException for
    // the rollback rule where create throws InvalidInputException.

    [Theory]
    [InlineData("deployment group create")]
    [InlineData("deployment group what-if")]
    [InlineData("deployment group validate")]
    public async Task RunAsync_DeploymentGroupBlankResourceGroup_ThrowsInvalidInput(string command)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, ["--resource-group", "", "--template-uri", TemplateUri], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("ResourceGroupName must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupCreateBothRollbackOptions_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "deployment group create",
            ["--resource-group", "rg", "--template-uri", TemplateUri, "--rollback-on-error", "--rollback-to-deployment-name", "previous"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentGroupValidateBothRollbackOptions_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "deployment group validate",
            ["--resource-group", "rg", "--template-uri", TemplateUri, "--rollback-on-error", "--rollback-to-deployment-name", "previous"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── deployment mg create, what-if and validate ─────────────────────────────────────────────
    // Current behaviour: these messages name the PowerShell parameters.

    [Theory]
    [InlineData("deployment mg create")]
    [InlineData("deployment mg what-if")]
    [InlineData("deployment mg validate")]
    public async Task RunAsync_DeploymentMgBlankManagementGroupId_ThrowsInvalidInput(string command)
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, command, ["--management-group-id", "", "--location", "westus", "--template-uri", TemplateUri], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("ManagementGroupId must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("deployment mg create")]
    [InlineData("deployment mg what-if")]
    [InlineData("deployment mg validate")]
    public async Task RunAsync_DeploymentMgBlankLocation_ThrowsInvalidInput(string command)
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, command, ["--management-group-id", "mg", "--location", "", "--template-uri", TemplateUri], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Location must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── deployment tenant wait ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitWithoutCondition_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "deployment tenant wait", ["--name", "deployment"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_DeploymentTenantWaitInvalidCustomExpression_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "deployment tenant wait", ["--name", "deployment", "--custom", "[["], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "--custom expression '[[' is not valid JMESPath: Error(1, 1): syntax, near ''.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // A zero timeout ends the wait before the first poll.
    [Fact]
    public async Task RunAsync_DeploymentTenantWaitZeroTimeout_ThrowsInvalidOperationException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "deployment tenant wait", ["--name", "deployment", "--created", "--timeout", "0"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Wait operation timed-out after 0 seconds", Assert.IsType<InvalidOperationException>(exception).Message);
    }
}
