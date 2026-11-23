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
/// Runs Key Vault certificate CLI commands through the real engine and checks the error for each rejected input. The
/// wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class CertificateCommandTests : IDisposable
{
    private const string ExtensionName = "KeyVault";
    private const string InvalidCertificateNameMessage = "Certificate names must contain 1-127 letters, digits or hyphens.";
    private const string InvalidCertificateVersionMessage = "Certificate version must be an alphanumeric version identifier.";
    private const string UnreadableInputFileMessage = "The certificate input file could not be read.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public CertificateCommandTests() => _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── certificate name, version and page size (shared by the certificate commands) ───────────

    [Theory]
    [InlineData("keyvault", "certificate", "create")]
    [InlineData("keyvault", "certificate", "import", "--file", "certificate.pfx")]
    [InlineData("keyvault", "certificate", "show")]
    [InlineData("keyvault", "certificate", "list-versions")]
    [InlineData("keyvault", "certificate", "set-attributes")]
    [InlineData("keyvault", "certificate", "delete")]
    public async Task RunAsync_InvalidCertificateName_ThrowsArgumentException(params string[] commandLine)
    {
        string invalidCertificateName = "my_certificate";
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault", "--name", invalidCertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "certificate", "show")]
    [InlineData("keyvault", "certificate", "set-attributes")]
    public async Task RunAsync_InvalidCertificateVersion_ThrowsArgumentException(params string[] commandLine)
    {
        string nonAlphanumericVersion = "not-a-version";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [.. commandLine, "--vault-name", "myvault", "--name", "certificate", "--version", nonAlphanumericVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(InvalidCertificateVersionMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    // Download checks the destination before the name and version, so it needs a destination that does not exist.
    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidName_ThrowsArgumentException()
    {
        string invalidCertificateName = "my_certificate";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "download",
                "--vault-name", "myvault",
                "--name", invalidCertificateName,
                "--file", Path.Combine(_missingDirectory, "certificate.pem"),
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(InvalidCertificateNameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidVersion_ThrowsArgumentException()
    {
        string nonAlphanumericVersion = "not-a-version";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "download",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--version", nonAlphanumericVersion,
                "--file", Path.Combine(_missingDirectory, "certificate.pem"),
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(InvalidCertificateVersionMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "certificate", "list", "--maxresults", "26")]
    [InlineData("keyvault", "certificate", "list-versions", "--name", "certificate", "--maxresults", "0")]
    public async Task RunAsync_PageSizeOutOfRange_ThrowsArgumentException(params string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Page size must be between 1 and 25.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── attributes and tags (shared by the certificate commands that write them) ───────────────

    [Theory]
    [InlineData("keyvault", "certificate", "create", "--name", "certificate")]
    [InlineData("keyvault", "certificate", "import", "--name", "certificate", "--file", "certificate.pfx")]
    [InlineData("keyvault", "certificate", "set-attributes", "--name", "certificate")]
    public async Task RunAsync_InvalidEnabled_ThrowsArgumentException(params string[] commandLine)
    {
        string nonBooleanEnabled = "yes";
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault", "--enabled", nonBooleanEnabled]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault", "certificate", "create", "--name", "certificate", "--expires", "2040-01-01")]
    [InlineData("keyvault", "certificate", "import", "--name", "certificate", "--file", "certificate.pfx", "--not-before", "tomorrow")]
    [InlineData("keyvault", "certificate", "set-attributes", "--name", "certificate", "--expires", "tomorrow")]
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
    [InlineData("keyvault", "certificate", "create", "--name", "certificate")]
    [InlineData("keyvault", "certificate", "import", "--name", "certificate", "--file", "certificate.pfx")]
    [InlineData("keyvault", "certificate", "set-attributes", "--name", "certificate")]
    public async Task RunAsync_TagWithoutName_ThrowsArgumentException(params string[] commandLine)
    {
        string tagWithoutName = "=value";
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--vault-name", "myvault", "--tags", tagWithoutName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate create ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateCreateTimeoutOutOfRange_ThrowsArgumentException()
    {
        string timeoutAboveMaximum = "3601";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", "myvault", "--name", "certificate", "--timeout", timeoutAboveMaximum]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Timeout must be between 1 and 3600 seconds.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateWithoutPolicy_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", "myvault", "--name", "certificate"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("An explicit certificate policy is required.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreatePolicyAndPolicyFile_ThrowsArgumentException()
    {
        string policy = "{}";
        string policyFile = "policy.json";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--policy", policy,
                "--policy-file", policyFile,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify one policy source: JSON, file or typed policy.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateInvalidPolicyJson_ThrowsArgumentException()
    {
        string invalidPolicyJson = "not json";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", "myvault", "--name", "certificate", "--policy", invalidPolicyJson]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreatePolicyOver1MiB_ThrowsArgumentException()
    {
        string policyOver1MiB = new string('a', (1024 * 1024) + 1);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--policy", policyOver1MiB,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Policy JSON exceeds 1 MiB.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateMissingPolicyFile_ThrowsArgumentException()
    {
        string missingPolicyFile = Path.Combine(_missingDirectory, "missing.json");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--policy-file", missingPolicyFile,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(UnreadableInputFileMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate import ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateImportPasswordAndPasswordFile_ThrowsArgumentException()
    {
        string password = "secret";
        string passwordFile = "password.txt";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "import",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--file", "certificate.pfx",
                "--password", password,
                "--password-file", passwordFile,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify only one certificate password source.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateImportMissingFile_ThrowsArgumentException()
    {
        string missingFile = Path.Combine(_missingDirectory, "missing.pfx");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "import",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--file", missingFile,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(UnreadableInputFileMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate set-attributes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateSetAttributesWithoutUpdate_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "set-attributes", "--vault-name", "myvault", "--name", "certificate"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify at least one certificate attribute, tag or policy update.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate download ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateDownloadBlankFile_ThrowsArgumentException()
    {
        string blankFile = "";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "download", "--vault-name", "myvault", "--name", "certificate", "--file", blankFile]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify a destination file.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidEncoding_ThrowsArgumentException()
    {
        string invalidEncoding = "PFX";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "download",
                "--vault-name", "myvault",
                "--name", "certificate",
                "--file", "certificate.pfx",
                "--encoding", invalidEncoding,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Certificate encoding must be PEM or DER.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate delete ─────────────────────────────────────────────────────────────────────
    // Current behaviour: the confirmation message mentions PowerShell ShouldProcess on the CLI.

    [Fact]
    public async Task RunAsync_CertificateDeleteWithoutYes_ThrowsArgumentException()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "delete", "--vault-name", "myvault", "--name", "certificate"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Deletion affects all certificate versions. Confirm with --yes or PowerShell ShouldProcess.",
            Assert.IsType<ArgumentException>(exception).Message);
    }
}
