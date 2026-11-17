// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs resource group CLI commands through the real engine and checks the error for each rejected input. The wire
/// mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class GroupCommandTests : IDisposable
{
    private const string ExtensionName = "Resources";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public GroupCommandTests() => _engine = CommandTestHelpers.CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── group delete ───────────────────────────────────────────────────────────────────────────
    // Current behaviour: the message mentions a resource group id, which the CLI command does not accept.

    [Fact]
    public async Task RunAsync_GroupDeleteBlankName_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "group delete", ["--name", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Specify exactly one of resource group name or resource group id.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── group update ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupUpdateTagWithoutKey_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "group update", ["--name", "rg", "--tags", "=value"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid tag '=value'. Tag keys cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSetWithoutValue_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "group update", ["--name", "rg", "--set", "managedBy"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Invalid --set expression 'managedBy'. Expected PATH=VALUE.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSetUnsupportedPath_ThrowsInvalidInput()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "group update", ["--name", "rg", "--set", "location=westus"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The --set path 'location' cannot be updated by the resource group PATCH API. Supported roots are name, managedBy, and tags.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }
}
