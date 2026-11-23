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
/// Runs Key Vault CLI commands through the real engine and checks the error for each rejected vault name or URL. The
/// wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class VaultSelectionCommandTests : IDisposable
{
    private const string ExtensionName = "KeyVault";
    private const string VaultName = "myvault";
    private const string PublicCloudVaultUrlMessage = "Use the root HTTPS URL of an Azure public-cloud vault.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public VaultSelectionCommandTests() => _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── vault selection ────────────────────────────────────────────────────────────────────────

    // Current behaviour: with no vault the message asks for a root HTTPS URL rather than saying a vault is required.
    [Fact]
    public async Task RunAsync_WithoutVault_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "show", "--name", "secret"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(PublicCloudVaultUrlMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_ExistingProfileWithoutVault_ThrowsArgumentException()
    {
        string existingProfile = "test";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--name", "secret", "--profile", existingProfile]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(PublicCloudVaultUrlMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_VaultNameAndVaultUrl_ThrowsArgumentException()
    {
        string vaultUrl = "https://myvault.vault.azure.net/";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--name", "secret", "--vault-name", VaultName, "--vault-url", vaultUrl]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify either a vault name or a vault URL, not both.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_InvalidVaultName_ThrowsArgumentException()
    {
        string vaultNameStartingWithDigit = "1vault";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--name", "secret", "--vault-name", vaultNameStartingWithDigit]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RelativeVaultUrl_ThrowsArgumentException()
    {
        string relativeVaultUrl = "myvault";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--name", "secret", "--vault-url", relativeVaultUrl]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The vault URL must be an absolute HTTPS URL.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_NonPublicCloudVaultUrl_ThrowsArgumentException()
    {
        string nonPublicCloudVaultUrl = "https://myvault.vault.azure.cn/";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--name", "secret", "--vault-url", nonPublicCloudVaultUrl]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(PublicCloudVaultUrlMessage, Assert.IsType<ArgumentException>(exception).Message);
    }
}
