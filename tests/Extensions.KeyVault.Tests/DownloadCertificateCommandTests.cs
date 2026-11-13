using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class DownloadCertificateCommandTests
{
    [Theory]
    [InlineData("DER")]
    [InlineData("PEM")]
    public async Task DownloadWritesOnlyPublicCertificateAndRefusesOverwrite(string encoding)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer");
        try
        {
            var command = new DownloadCertificateCommand { CertificateName = "test", File = destination, Encoding = encoding };
            int calls = 0;
            Initialize(command, _ => { calls++; return Response(JsonSerializer.Serialize(new { cer = certificate.RawData })); });
            await command.ProcessAsync();
            using var downloaded = encoding == "DER"
                ? X509CertificateLoader.LoadCertificateFromFile(destination)
                : X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(destination));
            Assert.Equal(certificate.Thumbprint, downloaded.Thumbprint);
            Assert.False(downloaded.HasPrivateKey);
            await Assert.ThrowsAsync<IOException>(() => command.ProcessAsync());
            Assert.Equal(1, calls);
        }
        finally { File.Delete(destination); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cer\":\"AQID\"}")]
    public async Task InvalidDownloadCreatesNoFile(string response)
    {
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination };
        Initialize(command, _ => Response(response));
        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ProcessAsync());
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public Task CertificateName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(
            new DownloadCertificateCommand { CertificateName = "bad_name", File = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer") },
            "Certificate names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task CertificateVersion_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(
            new DownloadCertificateCommand { CertificateName = "test", CertificateVersion = "bad-version", File = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer") },
            "Certificate version must be an alphanumeric version identifier.");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public Task MissingDestination_ThrowsBeforeNetwork(string file) =>
        AssertRejectedBeforeNetworkAsync(new DownloadCertificateCommand { CertificateName = "test", File = file },
            "Specify a destination file.");

    [Fact]
    public Task UnsupportedEncoding_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(
            new DownloadCertificateCommand { CertificateName = "test", File = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer"), Encoding = "PFX" },
            "Certificate encoding must be PEM or DER.");

    [Fact]
    public async Task ExistingDestination_ThrowsBeforeNetwork()
    {
        string destination = Path.GetTempFileName();
        try
        {
            await AssertRejectedBeforeNetworkAsync<IOException>(new DownloadCertificateCommand { CertificateName = "test", File = destination },
                "The certificate destination already exists. Choose a new path.");
        }
        finally { File.Delete(destination); }
    }
}
