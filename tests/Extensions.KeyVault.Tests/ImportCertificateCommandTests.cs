using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ImportCertificateCommandTests
{
    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ImportPreservesFileContentAndClearsSensitiveReferences(string format)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        const string password = "synthetic-password";
        byte[] bytes = format == "PFX" ? certificate.Export(X509ContentType.Pfx, password)
            : System.Text.Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + (format == "PEM"
                ? key.ExportPkcs8PrivateKeyPem()
                : key.ExportEncryptedPkcs8PrivateKeyPem(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))));
        string file = Path.GetTempFileName();
        using var secure = new System.Security.SecureString();
        foreach (char character in password) secure.AppendChar(character);
        try
        {
            await File.WriteAllBytesAsync(file, bytes);
            var command = new ImportCertificateCommand { CertificateName = "test", File = file, SecurePassword = secure, CertificateAttributesEnabled = false };
            Initialize(command, async message =>
            {
                using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
                Assert.Equal(password, body.RootElement.GetProperty("pwd").GetString());
                Assert.False(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
                Assert.Equal(format == "PFX" ? Convert.ToBase64String(bytes) : System.Text.Encoding.UTF8.GetString(bytes), body.RootElement.GetProperty("value").GetString());
                return await Response("{}");
            });
            await command.ProcessAsync();
            Assert.Null(command.Base64EncodedCertificate);
            Assert.Null(command.Password);
            Assert.Null(command.SecurePassword);
        }
        finally { File.Delete(file); CryptographicOperations.ZeroMemory(bytes); }
    }

    [Fact]
    public async Task ImportFailureClearsPasswordAndDoesNotExposeIt()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, [1, 2, 3]);
            var command = new ImportCertificateCommand { CertificateName = "test", File = file, Password = "private-fixture" };
            Initialize(command, _ => throw new Exception("Network must not run."));
            var error = await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
            Assert.DoesNotContain("private-fixture", error.ToString());
            Assert.Null(command.Password);
            Assert.Null(command.Base64EncodedCertificate);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task PasswordFile_IsValidated()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, new string('a', 16 * 1024 + 1));
            await AssertRejectedBeforeNetworkAsync(
                new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", PasswordFile = file },
                "The input file exceeds the supported size limit.");
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public Task CertificateFile_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(
            new ImportCertificateCommand { CertificateName = "test", File = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing") },
            "The certificate input file could not be read.");

    [Fact]
    public Task Policy_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", Policy = "[]" },
            "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.");

    [Fact]
    public Task CertificateName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "bad_name", Base64EncodedCertificate = "AQID" },
            "Certificate names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task Enabled_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", Enabled = "maybe" },
            "Enabled must be true or false.");

    [Fact]
    public Task Expires_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", Expires = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task NotBefore_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", NotBefore = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task Tags_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID", TagsInput = ["novalue"] },
            "Tags must use NAME=VALUE syntax.");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NotExactlyOneCertificateSource_ThrowsBeforeNetwork(bool both) =>
        AssertRejectedBeforeNetworkAsync(
            new ImportCertificateCommand { CertificateName = "test", File = both ? "unused.pfx" : null, Base64EncodedCertificate = both ? "AQID" : null },
            "Specify exactly one certificate source: file or encoded certificate.");

    [Theory]
    [InlineData("password", "file")]
    [InlineData("password", "secure")]
    [InlineData("file", "secure")]
    public Task MultiplePasswordSources_ThrowsBeforeNetwork(string first, string second)
    {
        var secure = new System.Security.SecureString();
        secure.AppendChar('x');
        var command = new ImportCertificateCommand { CertificateName = "test", Base64EncodedCertificate = "AQID" };
        foreach (string source in new[] { first, second })
        {
            if (source == "password") command.Password = "fixture";
            if (source == "file") command.PasswordFile = "unused-password.txt";
            if (source == "secure") command.SecurePassword = secure;
        }
        return AssertRejectedBeforeNetworkAsync(command, "Specify only one certificate password source.");
    }

    [Fact]
    public async Task PemFileWithPkcs12PolicyContentType_ThrowsBeforeNetwork()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "-----BEGIN CERTIFICATE-----\nAQID\n-----END CERTIFICATE-----\n");
            await AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand
            {
                CertificateName = "test", File = file, Policy = """{"secret_props":{"contentType":"application/x-pkcs12"}}"""
            }, "The certificate file format conflicts with the policy content type.");
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task PemWithoutPkcs8PrivateKey_ThrowsBeforeNetwork()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, certificate.ExportCertificatePem() + "\n" + key.ExportRSAPrivateKeyPem());
            await AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", File = file },
                "PEM import requires a certificate and a PKCS#8 private key. Pending-CSR merge is not supported.");
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("wrong-password")]
    [InlineData("pem-mismatched-key")]
    public async Task InvalidCertificateOrPassword_ThrowsBeforeNetwork(string scenario)
    {
        using var key = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        byte[] bytes = scenario switch
        {
            "garbage" => [1, 2, 3],
            "wrong-password" => certificate.Export(X509ContentType.Pfx, "synthetic-password"),
            _ => System.Text.Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + otherKey.ExportPkcs8PrivateKeyPem())
        };
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, bytes);
            await AssertRejectedBeforeNetworkAsync(new ImportCertificateCommand { CertificateName = "test", File = file, Password = "other-password" },
                "The certificate file or password is invalid, or the file has no matching private key.");
        }
        finally { File.Delete(file); }
    }
}
