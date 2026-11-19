// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;

namespace Cli.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault CLI commands through the real engine and checks the error for each rejected vault name or URL. The
/// wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class VaultSelectionCommandTests : IDisposable
{
    private const string ExtensionName = "KeyVault";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public VaultSelectionCommandTests() => _engine = CommandTestHelpers.CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── vault selection ────────────────────────────────────────────────────────────────────────

    // Current behaviour: with no vault the message asks for a root HTTPS URL rather than saying a vault is required.
    [Fact]
    public async Task RunAsync_WithoutVault_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "keyvault secret show", ["--name", "secret"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Use the root HTTPS URL of an Azure public-cloud vault.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_ExistingProfileWithoutVault_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "keyvault secret show", ["--name", "secret", "--profile", "test"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Use the root HTTPS URL of an Azure public-cloud vault.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_VaultNameAndVaultUrl_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault secret show",
            ["--name", "secret", "--vault-name", "myvault", "--vault-url", "https://myvault.vault.azure.net/"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify either a vault name or a vault URL, not both.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_InvalidVaultName_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "keyvault secret show", ["--name", "secret", "--vault-name", "1vault"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RelativeVaultUrl_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "keyvault secret show", ["--name", "secret", "--vault-url", "myvault"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The vault URL must be an absolute HTTPS URL.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_NonPublicCloudVaultUrl_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault secret show", ["--name", "secret", "--vault-url", "https://myvault.vault.azure.cn/"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Use the root HTTPS URL of an Azure public-cloud vault.", Assert.IsType<ArgumentException>(exception).Message);
    }
}
