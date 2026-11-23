// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsKeyVault;

using System.Collections;
using System.Management.Automation;
using System.Security;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using GetAzKeyVaultSecretCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.GetAzKeyVaultSecretCmdlet;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;
using RemoveAzKeyVaultSecretCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.RemoveAzKeyVaultSecretCmdlet;
using SetAzKeyVaultSecretCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.SetAzKeyVaultSecretCmdlet;
using UndoAzKeyVaultSecretRemovalCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.UndoAzKeyVaultSecretRemovalCmdlet;
using UpdateAzKeyVaultSecretCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.UpdateAzKeyVaultSecretCmdlet;

namespace Cmdlets.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault secret cmdlets in PowerShell through the real engine and checks the error for each rejected input. The
/// wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class SecretCommandTests : IDisposable
{
    private const string VaultName = "myvault";
    private const string InvalidSecretNameMessage = "Secret names must contain 1-127 letters, digits or hyphens.";
    private const string InvalidSecretVersionMessage = "Secret version must be an alphanumeric version identifier.";
    private const string InvalidDateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string InvalidTagNameMessage = "Tag names must be nonempty strings without '='.";
    private const string PageSizeAbove25Message =
        "Cannot validate argument on parameter 'MaxResults'. The 26 argument is greater than the maximum allowed range of 25. Supply an argument that is less than or equal to 25 and then try the command again.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public SecretCommandTests()
    {
        _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── secret name, version and page size (shared by the secret cmdlets) ──────────────────────
    // Remove-AzKeyVaultSecret has ConfirmImpact High, so it needs -Confirm:$false to run without a host to prompt.

    [Fact]
    public void Invoke_GetSecretInvalidName_ThrowsArgumentException()
    {
        string invalidSecretName = "my_secret";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidSecretName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetSecretVersionsInvalidName_ThrowsArgumentException()
    {
        string invalidSecretName = "my_secret";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidSecretName).AddParameter("IncludeVersions", true);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateSecretInvalidName_ThrowsArgumentException()
    {
        string invalidSecretName = "my_secret";
        using PowerShell shell = CreateShell<UpdateAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidSecretName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveSecretInvalidName_ThrowsArgumentException()
    {
        string invalidSecretName = "my_secret";
        using PowerShell shell = CreateShell<RemoveAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidSecretName).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UndoSecretRemovalInvalidName_ThrowsArgumentException()
    {
        string invalidSecretName = "my_secret";
        using PowerShell shell = CreateShell<UndoAzKeyVaultSecretRemovalCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidSecretName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_SetSecretInvalidName_ThrowsArgumentException()
    {
        string invalidSecretName = "my_secret";
        using PowerShell shell = CreateShell<SetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidSecretName).AddParameter("SecretValue", ToSecureString("secret-value"));

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetSecretInvalidVersion_ThrowsArgumentException()
    {
        string nonAlphanumericVersion = "not-a-version";
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "secret").AddParameter("Version", nonAlphanumericVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretVersionMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateSecretInvalidVersion_ThrowsArgumentException()
    {
        string nonAlphanumericVersion = "not-a-version";
        using PowerShell shell = CreateShell<UpdateAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "secret").AddParameter("Version", nonAlphanumericVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidSecretVersionMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateRange] rejects the page size while binding, so the extension's page size check is never reached. The
    // binder throws the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_GetSecretPageSizeAbove25_ThrowsParameterBindingException()
    {
        int pageSizeAbove25 = 26;
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("MaxResults", pageSizeAbove25);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PageSizeAbove25Message, Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    [Fact]
    public void Invoke_GetSecretVersionsPageSizeAbove25_ThrowsParameterBindingException()
    {
        int pageSizeAbove25 = 26;
        using PowerShell shell = CreateShell<GetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "secret")
            .AddParameter("IncludeVersions", true)
            .AddParameter("MaxResults", pageSizeAbove25);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PageSizeAbove25Message, Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // ── attributes and tags (shared by the secret cmdlets that write them) ─────────────────────
    // Dates are DateTimeOffset and Enabled is bool, so only a date outside the API's range reaches the extension.

    [Fact]
    public void Invoke_SetSecretDateAfter2038_ThrowsArgumentException()
    {
        DateTimeOffset expiresAfter2038 = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using PowerShell shell = CreateShell<SetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "secret")
            .AddParameter("SecretValue", ToSecureString("secret-value"))
            .AddParameter("Expires", expiresAfter2038);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidDateMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateSecretDateBefore1970_ThrowsArgumentException()
    {
        DateTimeOffset notBeforeBefore1970 = new(1969, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using PowerShell shell = CreateShell<UpdateAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "secret").AddParameter("NotBefore", notBeforeBefore1970);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidDateMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_SetSecretTagNameWithEquals_ThrowsPSArgumentException()
    {
        Hashtable tagNameWithEquals = new() { ["name=value"] = "value" };
        using PowerShell shell = CreateShell<SetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "secret")
            .AddParameter("SecretValue", ToSecureString("secret-value"))
            .AddParameter("Tag", tagNameWithEquals);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidTagNameMessage, Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateSecretTagWithoutName_ThrowsPSArgumentException()
    {
        Hashtable tagWithoutName = new() { [""] = "value" };
        using PowerShell shell = CreateShell<UpdateAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "secret").AddParameter("Tag", tagWithoutName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidTagNameMessage, Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Set-AzKeyVaultSecret ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_SetSecretValueOver25KiB_ThrowsArgumentException()
    {
        SecureString secretValueOver25KiB = ToSecureString(new string('a', (25 * 1024) + 1));
        using PowerShell shell = CreateShell<SetAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "secret").AddParameter("SecretValue", secretValueOver25KiB);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Secret values must not exceed 25 KiB.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Update-AzKeyVaultSecret ────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_UpdateSecretWithoutUpdate_ThrowsArgumentException()
    {
        using PowerShell shell = CreateShell<UpdateAzKeyVaultSecretCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "secret");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Specify at least one attribute, content type or tag update.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
