// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsKeyVault;

using System.Management.Automation;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using GetAzKeyVaultSecretCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.GetAzKeyVaultSecretCmdlet;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;

namespace Cmdlets.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault cmdlets in PowerShell through the real engine and checks the error for each rejected vault name or URL.
/// The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class VaultSelectionCommandTests : IDisposable
{
    private const string VaultName = "myvault";
    private const string PublicCloudVaultUrlMessage = "Use the root HTTPS URL of an Azure public-cloud vault.";
    private const string InvalidVaultNameMessage =
        "Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public VaultSelectionCommandTests()
    {
        _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── vault selection ────────────────────────────────────────────────────────────────────────

    // Current behaviour: with no vault the message asks for a root HTTPS URL rather than saying a vault is required.
    [Fact]
    public void Invoke_WithoutVault_ThrowsArgumentException()
    {
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PublicCloudVaultUrlMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ExistingProfileWithoutVault_ThrowsArgumentException()
    {
        string existingProfile = "test";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret").AddParameter("Profile", existingProfile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PublicCloudVaultUrlMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_VaultNameAndVaultUrl_ThrowsArgumentException()
    {
        string vaultUrl = "https://myvault.vault.azure.net/";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret").AddParameter("VaultName", VaultName).AddParameter("VaultUrl", vaultUrl);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Specify either a vault name or a vault URL, not both.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvalidVaultName_ThrowsArgumentException()
    {
        string vaultNameStartingWithDigit = "1vault";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret").AddParameter("VaultName", vaultNameStartingWithDigit);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidVaultNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // VaultName has no [ValidateNotNullOrEmpty], so a blank name reaches the extension's vault name check.
    [Fact]
    public void Invoke_BlankVaultName_ThrowsArgumentException()
    {
        string blankVaultName = "";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret").AddParameter("VaultName", blankVaultName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidVaultNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RelativeVaultUrl_ThrowsArgumentException()
    {
        string relativeVaultUrl = "myvault";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret").AddParameter("VaultUrl", relativeVaultUrl);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The vault URL must be an absolute HTTPS URL.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NonPublicCloudVaultUrl_ThrowsArgumentException()
    {
        string nonPublicCloudVaultUrl = "https://myvault.vault.azure.cn/";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("Name", "secret").AddParameter("VaultUrl", nonPublicCloudVaultUrl);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PublicCloudVaultUrlMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
