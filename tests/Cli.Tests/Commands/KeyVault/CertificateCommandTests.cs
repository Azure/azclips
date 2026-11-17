// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;

namespace Cli.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault certificate CLI commands through the real engine and checks the error for each rejected input. The
/// wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class CertificateCommandTests : IDisposable
{
    private const string ExtensionName = "KeyVault";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public CertificateCommandTests() => _engine = CommandTestHelpers.CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── certificate name, version and page size (shared by the certificate commands) ───────────

    [Theory]
    [InlineData("keyvault certificate create", new string[0])]
    [InlineData("keyvault certificate import", new[] { "--file", "certificate.pfx" })]
    [InlineData("keyvault certificate show", new string[0])]
    [InlineData("keyvault certificate list-versions", new string[0])]
    [InlineData("keyvault certificate set-attributes", new string[0])]
    [InlineData("keyvault certificate delete", new string[0])]
    public async Task RunAsync_InvalidCertificateName_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, ["--vault-name", "myvault", "--name", "my_certificate", .. args], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault certificate show", new string[0])]
    [InlineData("keyvault certificate set-attributes", new string[0])]
    public async Task RunAsync_InvalidCertificateVersion_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, command, ["--vault-name", "myvault", "--name", "certificate", "--version", "not-a-version", .. args], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Certificate version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // Download checks the destination before the name and version, so it needs a destination that does not exist.
    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidName_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate download",
            ["--vault-name", "myvault", "--name", "my_certificate", "--file", Path.Combine(_missingDirectory, "certificate.pem")],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidVersion_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate download",
            ["--vault-name", "myvault", "--name", "certificate", "--version", "not-a-version", "--file", Path.Combine(_missingDirectory, "certificate.pem")],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Certificate version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault certificate list", new[] { "--maxresults", "26" })]
    [InlineData("keyvault certificate list-versions", new[] { "--name", "certificate", "--maxresults", "0" })]
    public async Task RunAsync_PageSizeOutOfRange_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, ["--vault-name", "myvault", .. args], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Page size must be between 1 and 25.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── attributes and tags (shared by the certificate commands that write them) ───────────────

    [Theory]
    [InlineData("keyvault certificate create", new[] { "--name", "certificate" })]
    [InlineData("keyvault certificate import", new[] { "--name", "certificate", "--file", "certificate.pfx" })]
    [InlineData("keyvault certificate set-attributes", new[] { "--name", "certificate" })]
    public async Task RunAsync_InvalidEnabled_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, ["--vault-name", "myvault", .. args, "--enabled", "yes"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault certificate create", new[] { "--name", "certificate", "--expires", "2040-01-01" })]
    [InlineData("keyvault certificate import", new[] { "--name", "certificate", "--file", "certificate.pfx", "--not-before", "tomorrow" })]
    [InlineData("keyvault certificate set-attributes", new[] { "--name", "certificate", "--expires", "tomorrow" })]
    public async Task RunAsync_InvalidDate_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, ["--vault-name", "myvault", .. args], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("keyvault certificate create", new[] { "--name", "certificate" })]
    [InlineData("keyvault certificate import", new[] { "--name", "certificate", "--file", "certificate.pfx" })]
    [InlineData("keyvault certificate set-attributes", new[] { "--name", "certificate" })]
    public async Task RunAsync_TagWithoutName_ThrowsArgumentException(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, ["--vault-name", "myvault", .. args, "--tags", "=value"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate create ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateCreateTimeoutOutOfRange_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault certificate create", ["--vault-name", "myvault", "--name", "certificate", "--timeout", "3601"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Timeout must be between 1 and 3600 seconds.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateWithoutPolicy_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault certificate create", ["--vault-name", "myvault", "--name", "certificate"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("An explicit certificate policy is required.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreatePolicyAndPolicyFile_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate create",
            ["--vault-name", "myvault", "--name", "certificate", "--policy", "{}", "--policy-file", "policy.json"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify one policy source: JSON, file or typed policy.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateInvalidPolicyJson_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault certificate create", ["--vault-name", "myvault", "--name", "certificate", "--policy", "not json"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreatePolicyOver1MiB_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate create",
            ["--vault-name", "myvault", "--name", "certificate", "--policy", new string('a', (1024 * 1024) + 1)],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Policy JSON exceeds 1 MiB.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateMissingPolicyFile_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate create",
            ["--vault-name", "myvault", "--name", "certificate", "--policy-file", Path.Combine(_missingDirectory, "missing.json")],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The certificate input file could not be read.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate import ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateImportPasswordAndPasswordFile_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate import",
            ["--vault-name", "myvault", "--name", "certificate", "--file", "certificate.pfx", "--password", "secret", "--password-file", "password.txt"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify only one certificate password source.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateImportMissingFile_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate import",
            ["--vault-name", "myvault", "--name", "certificate", "--file", Path.Combine(_missingDirectory, "missing.pfx")],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The certificate input file could not be read.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate set-attributes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateSetAttributesWithoutUpdate_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault certificate set-attributes", ["--vault-name", "myvault", "--name", "certificate"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify at least one certificate attribute, tag or policy update.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate download ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateDownloadBlankFile_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault certificate download", ["--vault-name", "myvault", "--name", "certificate", "--file", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify a destination file.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidEncoding_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "keyvault certificate download",
            ["--vault-name", "myvault", "--name", "certificate", "--file", "certificate.pfx", "--encoding", "PFX"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Certificate encoding must be PEM or DER.", Assert.IsType<ArgumentException>(exception).Message);
    }

    // ── certificate delete ─────────────────────────────────────────────────────────────────────
    // Current behaviour: the confirmation message mentions PowerShell ShouldProcess on the CLI.

    [Fact]
    public async Task RunAsync_CertificateDeleteWithoutYes_ThrowsArgumentException()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "keyvault certificate delete", ["--vault-name", "myvault", "--name", "certificate"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Deletion affects all certificate versions. Confirm with --yes or PowerShell ShouldProcess.",
            Assert.IsType<ArgumentException>(exception).Message);
    }
}
