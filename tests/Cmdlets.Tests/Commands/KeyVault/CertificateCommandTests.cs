// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsKeyVault;

using System.Collections;
using System.Management.Automation;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using AddAzKeyVaultCertificateCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.AddAzKeyVaultCertificateCmdlet;
using ExportAzKeyVaultCertificateCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.ExportAzKeyVaultCertificateCmdlet;
using GetAzKeyVaultCertificateCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.GetAzKeyVaultCertificateCmdlet;
using ImportAzKeyVaultCertificateCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.ImportAzKeyVaultCertificateCmdlet;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;
using RemoveAzKeyVaultCertificateCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.RemoveAzKeyVaultCertificateCmdlet;
using UndoAzKeyVaultCertificateRemovalCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.UndoAzKeyVaultCertificateRemovalCmdlet;
using UpdateAzKeyVaultCertificateCmdlet = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault.UpdateAzKeyVaultCertificateCmdlet;

namespace Cmdlets.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault certificate cmdlets in PowerShell through the real engine and checks the error for each rejected input.
/// The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class CertificateCommandTests : IDisposable
{
    private const string VaultName = "myvault";
    private const string InvalidCertificateNameMessage = "Certificate names must contain 1-127 letters, digits or hyphens.";
    private const string InvalidCertificateVersionMessage = "Certificate version must be an alphanumeric version identifier.";
    private const string UnreadableInputFileMessage = "The certificate input file could not be read.";
    private const string InvalidDateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string InvalidTagNameMessage = "Tag names must be nonempty strings without '='.";
    private const string PageSizeAbove25Message =
        "Cannot validate argument on parameter 'MaxResults'. The 26 argument is greater than the maximum allowed range of 25. Supply an argument that is less than or equal to 25 and then try the command again.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public CertificateCommandTests()
    {
        _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── certificate name, version and page size (shared by the certificate cmdlets) ────────────
    // Remove-AzKeyVaultCertificate has ConfirmImpact High, so it needs -Confirm:$false to run without a host to prompt.

    [Fact]
    public void Invoke_AddCertificateInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ImportCertificateInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<ImportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName).AddParameter("FilePath", "certificate.pfx");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetCertificateInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<GetAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetCertificateVersionsInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<GetAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName).AddParameter("IncludeVersions", true);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateCertificateInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<UpdateAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveCertificateInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<RemoveAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName).AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UndoCertificateRemovalInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<UndoAzKeyVaultCertificateRemovalCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", invalidCertificateName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetCertificateInvalidVersion_ThrowsArgumentException()
    {
        string nonAlphanumericVersion = "not-a-version";
        using PowerShell shell = CreateShell<GetAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Version", nonAlphanumericVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateVersionMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateCertificateInvalidVersion_ThrowsArgumentException()
    {
        string nonAlphanumericVersion = "not-a-version";
        using PowerShell shell = CreateShell<UpdateAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Version", nonAlphanumericVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateVersionMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateRange] rejects the page size while binding, so the extension's page size check is never reached. The
    // binder throws the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_GetCertificatePageSizeAbove25_ThrowsParameterBindingException()
    {
        int pageSizeAbove25 = 26;
        using PowerShell shell = CreateShell<GetAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("MaxResults", pageSizeAbove25);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PageSizeAbove25Message, Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    [Fact]
    public void Invoke_GetCertificateVersionsPageSizeAbove25_ThrowsParameterBindingException()
    {
        int pageSizeAbove25 = 26;
        using PowerShell shell = CreateShell<GetAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "certificate")
            .AddParameter("IncludeVersions", true)
            .AddParameter("MaxResults", pageSizeAbove25);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(PageSizeAbove25Message, Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // Export checks the destination before the name and version, so it needs a destination that does not exist.
    [Fact]
    public void Invoke_ExportCertificateInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        using PowerShell shell = CreateShell<ExportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", invalidCertificateName)
            .AddParameter("FilePath", Path.Combine(_missingDirectory, "certificate.pem"));

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ExportCertificateInvalidVersion_ThrowsArgumentException()
    {
        string nonAlphanumericVersion = "not-a-version";
        using PowerShell shell = CreateShell<ExportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "certificate")
            .AddParameter("Version", nonAlphanumericVersion)
            .AddParameter("FilePath", Path.Combine(_missingDirectory, "certificate.pem"));

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidCertificateVersionMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── attributes and tags (shared by the certificate cmdlets that write them) ────────────────
    // Dates are DateTimeOffset and Enabled is bool, so only a date outside the API's range reaches the extension.

    [Fact]
    public void Invoke_AddCertificateDateAfter2038_ThrowsArgumentException()
    {
        DateTimeOffset expiresAfter2038 = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Expires", expiresAfter2038);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidDateMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ImportCertificateDateAfter2038_ThrowsArgumentException()
    {
        DateTimeOffset expiresAfter2038 = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using PowerShell shell = CreateShell<ImportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "certificate")
            .AddParameter("FilePath", "certificate.pfx")
            .AddParameter("Expires", expiresAfter2038);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidDateMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateCertificateDateAfter2038_ThrowsArgumentException()
    {
        DateTimeOffset expiresAfter2038 = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using PowerShell shell = CreateShell<UpdateAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Expires", expiresAfter2038);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidDateMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_AddCertificateTagNameWithEquals_ThrowsPSArgumentException()
    {
        Hashtable tagNameWithEquals = new() { ["name=value"] = "value" };
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Tag", tagNameWithEquals);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidTagNameMessage, Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ImportCertificateTagNameWithEquals_ThrowsPSArgumentException()
    {
        Hashtable tagNameWithEquals = new() { ["name=value"] = "value" };
        using PowerShell shell = CreateShell<ImportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "certificate")
            .AddParameter("FilePath", "certificate.pfx")
            .AddParameter("Tag", tagNameWithEquals);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidTagNameMessage, Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_UpdateCertificateTagNameWithEquals_ThrowsPSArgumentException()
    {
        Hashtable tagNameWithEquals = new() { ["name=value"] = "value" };
        using PowerShell shell = CreateShell<UpdateAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Tag", tagNameWithEquals);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(InvalidTagNameMessage, Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Add-AzKeyVaultCertificate ──────────────────────────────────────────────────────────────

    // [ValidateRange] rejects the timeout while binding. The binder throws the internal
    // ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_AddCertificateTimeoutAbove3600_ThrowsParameterBindingException()
    {
        int timeoutAbove3600 = 3601;
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("TimeoutSeconds", timeoutAbove3600);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'TimeoutSeconds'. The 3601 argument is greater than the maximum allowed range of 3600. Supply an argument that is less than or equal to 3600 and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    [Fact]
    public void Invoke_AddCertificateWaitAndNoWait_ThrowsPSArgumentException()
    {
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Wait").AddParameter("NoWait");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Specify either Wait or NoWait, not both.",
            Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_AddCertificateWithoutPolicy_ThrowsArgumentException()
    {
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "An explicit certificate policy is required.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_AddCertificatePolicyAndPolicyPath_ThrowsArgumentException()
    {
        string policy = "{}";
        string policyPath = "policy.json";
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "certificate")
            .AddParameter("Policy", policy)
            .AddParameter("PolicyPath", policyPath);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Specify one policy source: JSON, file or typed policy.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_AddCertificateInvalidPolicyJson_ThrowsArgumentException()
    {
        string invalidPolicyJson = "not json";
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Policy", invalidPolicyJson);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_AddCertificatePolicyOver1MiB_ThrowsArgumentException()
    {
        string policyOver1MiB = new string('a', (1024 * 1024) + 1);
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("Policy", policyOver1MiB);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal("Policy JSON exceeds 1 MiB.", Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_AddCertificateMissingPolicyPath_ThrowsArgumentException()
    {
        string missingPolicyPath = Path.Combine(_missingDirectory, "missing.json");
        using PowerShell shell = CreateShell<AddAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("PolicyPath", missingPolicyPath);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(UnreadableInputFileMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Import-AzKeyVaultCertificate ───────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_ImportCertificateMissingFile_ThrowsArgumentException()
    {
        string missingFilePath = Path.Combine(_missingDirectory, "missing.pfx");
        using PowerShell shell = CreateShell<ImportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate").AddParameter("FilePath", missingFilePath);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(UnreadableInputFileMessage, Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Export-AzKeyVaultCertificate ───────────────────────────────────────────────────────────

    // [ValidateSet] rejects the encoding while binding. The binder throws the internal
    // ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_ExportCertificateEncodingNotPemOrDer_ThrowsParameterBindingException()
    {
        string encodingNotPemOrDer = "PFX";
        using PowerShell shell = CreateShell<ExportAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName)
            .AddParameter("Name", "certificate")
            .AddParameter("FilePath", Path.Combine(_missingDirectory, "certificate.pem"))
            .AddParameter("Encoding", encodingNotPemOrDer);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'Encoding'. The argument \"PFX\" does not belong to the set \"PEM,DER\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // ── Update-AzKeyVaultCertificate ───────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_UpdateCertificateWithoutUpdate_ThrowsArgumentException()
    {
        using PowerShell shell = CreateShell<UpdateAzKeyVaultCertificateCmdlet>();
        shell.AddParameter("VaultName", VaultName).AddParameter("Name", "certificate");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Specify at least one certificate attribute, tag or policy update.",
            Assert.IsType<ArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
