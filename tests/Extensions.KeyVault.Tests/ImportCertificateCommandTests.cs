using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ImportCertificateCommandTests
{
    private const string Password = "synthetic-password";
    private const string InvalidCertificateMessage = "The certificate file or password is invalid, or the file has no matching private key.";
    private const string DateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string PasswordSourcesMessage = "Specify only one certificate password source.";
    private const string CertificateSourcesMessage = "Specify exactly one certificate source: file or encoded certificate.";
    private const string PolicyMessage = "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.";
    private const string Pkcs8Message = "PEM import requires a certificate and a PKCS#8 private key. Pending-CSR merge is not supported.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ProcessAsync_CertificateFile_SendsPasswordInBody(string format)
    {
        byte[] bytes = CreateCertificateFileBytes(format);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, bytes);
        using SecureString securePassword = SecureStringOf(Password);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, SecurePassword = securePassword, CertificateAttributesEnabled = false };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(Password, JsonNode.Parse(sentBodies.Single()!)!["pwd"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ProcessAsync_CertificateFile_SendsInheritedEnabledAttribute(string format)
    {
        byte[] bytes = CreateCertificateFileBytes(format);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, bytes);
        using SecureString securePassword = SecureStringOf(Password);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, SecurePassword = securePassword, CertificateAttributesEnabled = false };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.False(JsonNode.Parse(sentBodies.Single()!)!["attributes"]!["enabled"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ProcessAsync_CertificateFile_SendsFileContentAsValue(string format)
    {
        byte[] bytes = CreateCertificateFileBytes(format);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, bytes);
        using SecureString securePassword = SecureStringOf(Password);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, SecurePassword = securePassword, CertificateAttributesEnabled = false };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(ExpectedValue(format, bytes), JsonNode.Parse(sentBodies.Single()!)!["value"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ProcessAsync_CertificateFile_ClearsEncodedCertificate(string format)
    {
        byte[] bytes = CreateCertificateFileBytes(format);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, bytes);
        using SecureString securePassword = SecureStringOf(Password);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, SecurePassword = securePassword, CertificateAttributesEnabled = false };
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(command.Base64EncodedCertificate);
    }

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ProcessAsync_CertificateFile_ClearsPassword(string format)
    {
        byte[] bytes = CreateCertificateFileBytes(format);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, bytes);
        using SecureString securePassword = SecureStringOf(Password);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, SecurePassword = securePassword, CertificateAttributesEnabled = false };
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(command.Password);
    }

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ProcessAsync_CertificateFile_ClearsSecurePassword(string format)
    {
        byte[] bytes = CreateCertificateFileBytes(format);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, bytes);
        using SecureString securePassword = SecureStringOf(Password);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, SecurePassword = securePassword, CertificateAttributesEnabled = false };
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(command.SecurePassword);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateFileWithPassword_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3]);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "private-fixture" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(InvalidCertificateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateFileWithPassword_OmitsPasswordFromException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3]);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "private-fixture" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.DoesNotContain("private-fixture", exception!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateFileWithPassword_ClearsPassword()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3]);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "private-fixture" };
        Initialize(command, _mockPipeline);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Null(command.Password);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateFileWithPassword_ClearsEncodedCertificate()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3]);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "private-fixture" };
        Initialize(command, _mockPipeline);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Null(command.Base64EncodedCertificate);
    }

    [Fact]
    public async Task ProcessAsync_PasswordFileOverLimit_ThrowsArgumentException()
    {
        using TempFile passwordFile = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(passwordFile.Path, new string('a', 16 * 1024 + 1));
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", PasswordFile = passwordFile.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("The input file exceeds the supported size limit.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_MissingCertificateFile_ThrowsArgumentException()
    {
        using TempFile missing = TempFile.ReservePath(".missing");
        var command = new ImportCertificateCommand { CertificateName = "test", File = missing.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("The certificate input file could not be read.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NonObjectPolicy_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", Policy = "[]" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PolicyMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateName_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "bad_name", Base64EncodedCertificate = "AQID" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidEnabled_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", Enabled = "maybe" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidExpires_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", Expires = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidNotBefore_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", NotBefore = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_TagWithoutValue_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", TagsInput = ["novalue"] };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NoCertificateSource_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(CertificateSourcesMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_FileAndEncodedCertificate_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand { CertificateName = "test", File = "unused.pfx", Base64EncodedCertificate = "AQID" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(CertificateSourcesMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PasswordAndPasswordFile_ThrowsArgumentException()
    {
        var command = new ImportCertificateCommand
        {
            CertificateName = "test", Base64EncodedCertificate = "AQID", Password = "fixture", PasswordFile = "unused-password.txt"
        };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PasswordSourcesMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PasswordAndSecurePassword_ThrowsArgumentException()
    {
        using SecureString securePassword = SecureStringOf("x");
        var command = new ImportCertificateCommand
        {
            CertificateName = "test", Base64EncodedCertificate = "AQID", Password = "fixture", SecurePassword = securePassword
        };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PasswordSourcesMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PasswordFileAndSecurePassword_ThrowsArgumentException()
    {
        using SecureString securePassword = SecureStringOf("x");
        var command = new ImportCertificateCommand
        {
            CertificateName = "test", Base64EncodedCertificate = "AQID", PasswordFile = "unused-password.txt", SecurePassword = securePassword
        };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PasswordSourcesMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PemFileWithPkcs12PolicyContentType_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, "-----BEGIN CERTIFICATE-----\nAQID\n-----END CERTIFICATE-----\n");
        var command = new ImportCertificateCommand
        {
            CertificateName = "test", File = file.Path, Policy = """{"secret_props":{"contentType":"application/x-pkcs12"}}"""
        };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("The certificate file format conflicts with the policy content type.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PemWithoutPkcs8PrivateKey_ThrowsArgumentException()
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, certificate.ExportCertificatePem() + "\n" + key.ExportRSAPrivateKeyPem());
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(Pkcs8Message, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_GarbageCertificateFile_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3]);
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "other-password" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(InvalidCertificateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PfxWithWrongPassword_ThrowsArgumentException()
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, certificate.Export(X509ContentType.Pfx, Password));
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "other-password" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(InvalidCertificateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PemWithMismatchedPrivateKey_ThrowsArgumentException()
    {
        using RSA key = RSA.Create(2048);
        using RSA otherKey = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, certificate.ExportCertificatePem() + "\n" + otherKey.ExportPkcs8PrivateKeyPem());
        var command = new ImportCertificateCommand { CertificateName = "test", File = file.Path, Password = "other-password" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(InvalidCertificateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    private byte[] CreateCertificateFileBytes(string format)
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        return format switch
        {
            "PFX" => certificate.Export(X509ContentType.Pfx, Password),
            "PEM" => Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem()),
            _ => Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + key.ExportEncryptedPkcs8PrivateKeyPem(
                Password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)))
        };
    }

    private string ExpectedValue(string format, byte[] bytes) =>
        format == "PFX" ? Convert.ToBase64String(bytes) : Encoding.UTF8.GetString(bytes);
}