// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;

namespace Cli.Tests.Commands.ManagedIdentity;

/// <summary>
/// Runs managed identity federated credential CLI commands through the real engine and checks the error for each
/// rejected input. The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class FederatedCredentialCommandTests : IDisposable
{
    private const string ExtensionName = "ManagedServiceIdentity";
    private const string ClaimsMatchingExpressionMessage = "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public FederatedCredentialCommandTests() => _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── identity federated-credential create / update ──────────────────────────────────────────
    // Current behaviour: the message names the PowerShell-style property names instead of the CLI flags
    // --claims-matching-expression-version and --claims-matching-expression-value.

    [Theory]
    [InlineData("identity", "federated-credential", "create")]
    [InlineData("identity", "federated-credential", "update")]
    public async Task RunAsync_ClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput(params string[] commandLine)
    {
        string claimsMatchingExpressionVersion = "1";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                .. commandLine,
                "--resource-group", "rg",
                "--identity-name", "id",
                "--name", "fc",
                "--claims-matching-expression-version", claimsMatchingExpressionVersion,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(ClaimsMatchingExpressionMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CreateClaimsMatchingExpressionValueWithoutVersion_ThrowsInvalidInput()
    {
        string claimsMatchingExpressionValue = "claims['sub'] eq 'repo'";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "create",
                "--resource-group", "rg",
                "--identity-name", "id",
                "--name", "fc",
                "--claims-matching-expression-value", claimsMatchingExpressionValue,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(ClaimsMatchingExpressionMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CreateBlankClaimsMatchingExpressionValue_ThrowsInvalidInput()
    {
        string blankClaimsMatchingExpressionValue = " ";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "create",
                "--resource-group", "rg",
                "--identity-name", "id",
                "--name", "fc",
                "--cme-version", "1",
                "--cme-value", blankClaimsMatchingExpressionValue,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(ClaimsMatchingExpressionMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }
}
