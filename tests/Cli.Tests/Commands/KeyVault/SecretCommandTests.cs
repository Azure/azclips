// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;

namespace Cli.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault secret CLI commands through the real engine and checks the error for each rejected input. The wire
/// mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class SecretCommandTests : IDisposable
{
    private const string ExtensionName = "KeyVault";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public SecretCommandTests() => _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── secret name, version and page size (shared by the secret commands) ─────────────────────

    [Theory]
    [InlineData("keyvault", "secret", "set")]
    [InlineData("keyvault", "secret", "show")]
    [InlineData("keyvault", "secret", "list-versions")]
    [InlineData("keyvault", "secret", "set-attributes")]
    [InlineData("keyvault", "secret", "delete")]
    public async Task RunAsync_InvalidSecretName_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault", "--name", "my_secret"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "secret", "show")]
    [InlineData("keyvault", "secret", "set-attributes")]
    public async Task RunAsync_InvalidSecretVersion_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [.. commandLine, "--vault-name", "myvault", "--name", "secret", "--version", "not-a-version"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Secret version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "secret", "list", "--maxresults", "0")]
    [InlineData("keyvault", "secret", "list-versions", "--name", "secret", "--maxresults", "26")]
    public async Task RunAsync_PageSizeOutOfRange_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Page size must be between 1 and 25.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── attributes and tags (shared by the secret commands that write them) ────────────────────

    [Theory]
    [InlineData("keyvault", "secret", "set", "--name", "secret", "--value", "secret-value")]
    [InlineData("keyvault", "secret", "set-attributes", "--name", "secret")]
    public async Task RunAsync_InvalidEnabled_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault", "--enabled", "yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "secret", "set", "--name", "secret", "--value", "secret-value", "--expires", "tomorrow")]
    [InlineData("keyvault", "secret", "set-attributes", "--name", "secret", "--not-before", "2040-01-01")]
    public async Task RunAsync_InvalidDate_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "secret", "set", "--name", "secret", "--value", "secret-value")]
    [InlineData("keyvault", "secret", "set-attributes", "--name", "secret")]
    public async Task RunAsync_TagWithoutName_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault", "--tags", "=value"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── secret set ─────────────────────────────────────────────────────────────────────────────
    // Current behaviour: the value-source message mentions SecureString, which CLI users cannot pass.

    [Fact]
    public async Task RunAsync_SecretSetWithoutValueOrFile_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "set", "--vault-name", "myvault", "--name", "secret"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Specify exactly one secret value source: value, file or SecureString.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_SecretSetValueAndFile_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", "myvault", "--name", "secret", "--value", "secret-value", "--file", "secret.txt"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Specify exactly one secret value source: value, file or SecureString.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_SecretSetValueOver25KiB_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", "myvault", "--name", "secret", "--value", new string('a', (25 * 1024) + 1)]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Secret values must not exceed 25 KiB.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // Current behaviour: a file that does not exist is reported as one that could not be read as UTF-8.
    [Fact]
    public async Task RunAsync_SecretSetMissingFile_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", "myvault", "--name", "secret", "--file", Path.Combine(_missingDirectory, "missing.txt")]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The secret file could not be read as UTF-8.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── secret set-attributes ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretSetAttributesWithoutUpdate_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set-attributes", "--vault-name", "myvault", "--name", "secret"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify at least one attribute, content type or tag update.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── secret delete ──────────────────────────────────────────────────────────────────────────
    // Current behaviour: the confirmation message mentions PowerShell ShouldProcess on the CLI.

    [Fact]
    public async Task RunAsync_SecretDeleteWithoutYes_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "delete", "--vault-name", "myvault", "--name", "secret"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Deletion affects all secret versions. Confirm with --yes or PowerShell ShouldProcess.",
            Assert.IsType<ArgumentException>(exception).Message);
    }
}
