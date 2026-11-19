// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs rest CLI commands through the real engine and checks the error for each rejected input. The wire mock is
/// strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class RestCommandTests : IDisposable
{
    private const string ExtensionName = "Resources";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public RestCommandTests() => _engine = CommandTestHelpers.CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── rest ───────────────────────────────────────────────────────────────────────────────────
    // Current behaviour: these messages name the PowerShell parameters (Uri, SubscriptionId, ApiVersion, ...).

    [Fact]
    public async Task RunAsync_RestWithoutEndpoint_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", [], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "An endpoint is required: supply either a full URL (Uri) or the piece-wise ByParameters inputs " +
            "(SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion) to build an ARM URL.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestUriAndPieceWiseParameters_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "/subscriptions", "--resource-group", "rg"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Specify the endpoint either as a full URL (Uri) OR as the piece-wise parameters " +
            "(SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion), not both.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestUnsupportedMethod_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "/subscriptions", "--method", "TRACE"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Unsupported HTTP method 'TRACE'. Supported methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestHttpUrl_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "http://management.azure.com/subscriptions"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Absolute URL 'http://management.azure.com/subscriptions' must use https. The rest command only targets ARM over https in this release.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestNonArmHost_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "https://graph.microsoft.com/v1.0/me"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Host 'graph.microsoft.com' is not an allowed Azure Resource Manager endpoint. The rest command only targets ARM in this release. " +
            "Allowed hosts: management.azure.com, management.chinacloudapi.cn, management.microsoftazure.de, management.usgovcloudapi.net.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestPieceWiseWithoutApiVersion_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--resource-group", "rg"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "ApiVersion is required when building an ARM URL from piece-wise parameters.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestMoreNamesThanResourceTypes_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "rest",
            ["--resource-provider", "Microsoft.Storage", "--resource-type", "storageAccounts", "--name", "first", "second", "--api-version", "2023-01-01"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Name (2) must equal ResourceType (1) or be exactly one fewer (one trailing type without a name performs a collection GET on the last type).",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestBlankResourceType_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "rest", ["--resource-provider", "Microsoft.Storage", "--resource-type", " ", "--api-version", "2023-01-01"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "ResourceType segments must be non-empty; ResourceType element 0 was empty or whitespace.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestBlankName_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "rest",
            ["--resource-provider", "Microsoft.Storage", "--resource-type", "storageAccounts", "--name", " ", "--api-version", "2023-01-01"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Name segments must be non-empty; Name element 0 was empty or whitespace.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestUriParameterWithoutValue_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "/subscriptions", "--uri-parameters", "top"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid uri parameter 'top'. Expected KEY=VALUE format.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestUriParameterWithBlankName_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "/subscriptions", "--uri-parameters", " =5"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid uri parameter ' =5': parameter name cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestHeaderWithoutValue_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "/subscriptions", "--headers", "Accept"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid header 'Accept'. Expected KEY=VALUE format.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestHeaderWithBlankName_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "rest", ["--uri", "/subscriptions", "--headers", " =json"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid header ' =json': header name cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }
}
