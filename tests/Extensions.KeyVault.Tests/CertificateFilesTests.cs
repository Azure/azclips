using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

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

    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Read_FileOverLimit_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3, 4, 5]);

        Exception? exception = await Record.ExceptionAsync(() => CertificateFiles.Read(file.Path, 4, CancellationToken.None));

        Assert.Equal(SizeMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task Read_FileAtLimit_ReturnsBytes()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [1, 2, 3, 4]);

        byte[] bytes = await CertificateFiles.Read(file.Path, 4, CancellationToken.None);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
    }

    [Fact]
    public async Task Read_MissingFile_ThrowsArgumentException()
    {
        using TempFile missing = TempFile.ReservePath(".missing");

        Exception? exception = await Record.ExceptionAsync(() => CertificateFiles.Read(missing.Path, 4, CancellationToken.None));

        Assert.Equal(UnreadableMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ReadText_InvalidUtf8_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [0x7B, 0xFF, 0xFE, 0x7D]);

        Exception? exception = await Record.ExceptionAsync(() => CertificateFiles.ReadText(file.Path, 16, CancellationToken.None));

        Assert.Equal(Utf8Message, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task Policy_MultipleSources_ThrowsArgumentException(bool json, bool file, bool inherited)
    {
        string? policyJson = json ? "{}" : null;
        string? policyFile = file ? "unused-policy.json" : null;
        CertificatePolicy? inheritedPolicy = inherited ? new CertificatePolicy() : null;

        Exception? exception = await Record.ExceptionAsync(() => CertificateFiles.Policy(policyJson, policyFile, inheritedPolicy, _options, CancellationToken.None));

        Assert.Equal(SourcesMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task Policy_JsonOver1MiB_ThrowsArgumentException()
    {
        string json = new(' ', CertificateFiles.MaximumBytes + 1);

        Exception? exception = await Record.ExceptionAsync(() => CertificateFiles.Policy(json, null, null, _options, CancellationToken.None));

        Assert.Equal(PolicySizeMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task Policy_JsonOfExactly1MiB_ReturnsPolicy()
    {
        string json = "{}" + new string(' ', CertificateFiles.MaximumBytes - 2);

        CertificatePolicy? policy = await CertificateFiles.Policy(json, null, null, _options, CancellationToken.None);

        Assert.NotNull(policy);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"typo":true}""")]
    [InlineData("""{"issuer":{"name":"Self"},"issuer_parameters":{"name":"Unknown"}}""")]
    public async Task Policy_InvalidJson_ThrowsArgumentException(string json)
    {
        Exception? exception = await Record.ExceptionAsync(() => CertificateFiles.Policy(json, null, null, _options, CancellationToken.None));

        Assert.Equal(PolicyMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ValidatePkcs12_PfxWithPrivateKey_DoesNotThrow()
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        byte[] pfx = certificate.Export(X509ContentType.Pfx, Password);

        Exception? exception = Record.Exception(() => CertificateFiles.ValidatePkcs12(pfx, Password));

        Assert.Null(exception);
    }

    // ValidatePkcs12 throws CryptographicException; ImportCertificateCommand maps it to the user-facing
    // message. The loader's message is platform-specific, so only the exception type is asserted.
    [Fact]
    public void ValidatePkcs12_PfxWithoutPrivateKey_ThrowsCryptographicException()
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using X509Certificate2 publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        byte[] pfx = publicOnly.Export(X509ContentType.Pfx, Password);

        Exception? exception = Record.Exception(() => CertificateFiles.ValidatePkcs12(pfx, Password));

        Assert.IsAssignableFrom<CryptographicException>(exception);
    }

    [Fact]
    public void ValidatePkcs12_GarbageBytes_ThrowsCryptographicException()
    {
        byte[] pfx = [1, 2, 3];

        Exception? exception = Record.Exception(() => CertificateFiles.ValidatePkcs12(pfx, Password));

        Assert.IsAssignableFrom<CryptographicException>(exception);
    }

    [Fact]
    public void ValidatePkcs12_WrongPassword_ThrowsCryptographicException()
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        byte[] pfx = certificate.Export(X509ContentType.Pfx, Password);

        Exception? exception = Record.Exception(() => CertificateFiles.ValidatePkcs12(pfx, "other-password"));

        Assert.IsAssignableFrom<CryptographicException>(exception);
    }
}