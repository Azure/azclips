using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;

namespace Extensions.KeyVault.Tests;

public class CertificateFilesTests
{
    private const string SizeMessage = "The input file exceeds the supported size limit.";
    private const string UnreadableMessage = "The certificate input file could not be read.";
    private const string Utf8Message = "The input file must contain valid UTF-8.";
    private const string SourcesMessage = "Specify one policy source: JSON, file or typed policy.";
    private const string PolicySizeMessage = "Policy JSON exceeds 1 MiB.";
    private const string PolicyMessage = "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.";
    private const string Password = "synthetic-password";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Read_FileOverLimit_Throws()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, [1, 2, 3, 4, 5]);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => CertificateFiles.Read(file, 4, CancellationToken.None));

            Assert.Equal(SizeMessage, ex.Message);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Read_FileAtLimit_ReturnsBytes()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, [1, 2, 3, 4]);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await CertificateFiles.Read(file, 4, CancellationToken.None));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Read_MissingFile_Throws()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CertificateFiles.Read(missing, 4, CancellationToken.None));

        Assert.Equal(UnreadableMessage, ex.Message);
    }

    [Fact]
    public async Task ReadText_InvalidUtf8_Throws()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, [0x7B, 0xFF, 0xFE, 0x7D]);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => CertificateFiles.ReadText(file, 16, CancellationToken.None));

            Assert.Equal(Utf8Message, ex.Message);
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task Policy_MultipleSources_Throws(bool json, bool file, bool inherited)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CertificateFiles.Policy(
            json ? "{}" : null, file ? "unused-policy.json" : null, inherited ? new CertificatePolicy() : null, Options, CancellationToken.None));

        Assert.Equal(SourcesMessage, ex.Message);
    }

    [Fact]
    public async Task Policy_JsonOver1MiB_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CertificateFiles.Policy(
            new string(' ', CertificateFiles.MaximumBytes + 1), null, null, Options, CancellationToken.None));

        Assert.Equal(PolicySizeMessage, ex.Message);
    }

    [Fact]
    public async Task Policy_JsonOfExactly1MiB_IsAccepted()
    {
        string json = "{}" + new string(' ', CertificateFiles.MaximumBytes - 2);

        Assert.NotNull(await CertificateFiles.Policy(json, null, null, Options, CancellationToken.None));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"typo":true}""")]
    [InlineData("""{"issuer":{"name":"Self"},"issuer_parameters":{"name":"Unknown"}}""")]
    public async Task Policy_InvalidJson_Throws(string json)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CertificateFiles.Policy(json, null, null, Options, CancellationToken.None));

        Assert.Equal(PolicyMessage, ex.Message);
    }

    [Fact]
    public void ValidatePkcs12_PfxWithPrivateKey_DoesNotThrow()
    {
        using var key = RSA.Create(2048);
        using var certificate = CreateCertificate(key);
        byte[] pfx = certificate.Export(X509ContentType.Pfx, Password);

        Assert.Null(Record.Exception(() => CertificateFiles.ValidatePkcs12(pfx, Password)));
    }

    // ValidatePkcs12 throws CryptographicException; ImportCertificateCommand maps it to the user-facing
    // message. The loader's message is platform-specific, so only the exception type is asserted.
    [Fact]
    public void ValidatePkcs12_PfxWithoutPrivateKey_Throws()
    {
        using var key = RSA.Create(2048);
        using var certificate = CreateCertificate(key);
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        byte[] pfx = publicOnly.Export(X509ContentType.Pfx, Password);

        Assert.ThrowsAny<CryptographicException>(() => CertificateFiles.ValidatePkcs12(pfx, Password));
    }

    [Fact]
    public void ValidatePkcs12_GarbageBytes_Throws() =>
        Assert.ThrowsAny<CryptographicException>(() => CertificateFiles.ValidatePkcs12([1, 2, 3], Password));

    [Fact]
    public void ValidatePkcs12_WrongPassword_Throws()
    {
        using var key = RSA.Create(2048);
        using var certificate = CreateCertificate(key);
        byte[] pfx = certificate.Export(X509ContentType.Pfx, Password);

        Assert.ThrowsAny<CryptographicException>(() => CertificateFiles.ValidatePkcs12(pfx, "other-password"));
    }

    private static X509Certificate2 CreateCertificate(RSA key)
    {
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}