using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class DownloadCertificateCommandTests
{
    private const string ExistsMessage = "The certificate destination already exists. Choose a new path.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Theory]
    [InlineData("DER")]
    [InlineData("PEM")]
    public async Task ProcessAsync_Encoding_WritesCertificateWithSameThumbprint(string encoding)
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path, Encoding = encoding };
        _mockPipeline.RespondWith(JsonSerializer.Serialize(new { cer = certificate.RawData }));
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(certificate.Thumbprint, ReadDownloadedCertificate(destination.Path, encoding).Thumbprint);
    }

    [Theory]
    [InlineData("DER")]
    [InlineData("PEM")]
    public async Task ProcessAsync_Encoding_WritesCertificateWithoutPrivateKey(string encoding)
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path, Encoding = encoding };
        _mockPipeline.RespondWith(JsonSerializer.Serialize(new { cer = certificate.RawData }));
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.False(ReadDownloadedCertificate(destination.Path, encoding).HasPrivateKey);
    }

    [Theory]
    [InlineData("DER")]
    [InlineData("PEM")]
    public async Task ProcessAsync_SecondDownloadToSamePath_ThrowsIOException(string encoding)
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path, Encoding = encoding };
        _mockPipeline.RespondWith(JsonSerializer.Serialize(new { cer = certificate.RawData }));
        Initialize(command, _mockPipeline);
        await command.ProcessAsync();

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(ExistsMessage, Assert.IsType<IOException>(exception).Message);
    }

    [Theory]
    [InlineData("DER")]
    [InlineData("PEM")]
    public async Task ProcessAsync_SecondDownloadToSamePath_SendsNoSecondRequest(string encoding)
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(key);
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path, Encoding = encoding };
        _mockPipeline.RespondWith(JsonSerializer.Serialize(new { cer = certificate.RawData }));
        Initialize(command, _mockPipeline);
        await command.ProcessAsync();

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Single(_mockPipeline.SentRequests());
    }

    [Theory]
    [InlineData("{}", "The response contains no public certificate to download.")]
    [InlineData("{\"cer\":\"AQID\"}", "The response contains an invalid public certificate.")]
    public async Task ProcessAsync_ResponseWithoutValidCertificate_ThrowsInvalidOperationException(string response, string message)
    {
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path };
        _mockPipeline.RespondWith(response);
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(message, Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cer\":\"AQID\"}")]
    public async Task ProcessAsync_ResponseWithoutValidCertificate_CreatesNoFile(string response)
    {
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path };
        _mockPipeline.RespondWith(response);
        Initialize(command, _mockPipeline);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.False(File.Exists(destination.Path));
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateName_ThrowsArgumentException()
    {
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "bad_name", File = destination.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateVersion_ThrowsArgumentException()
    {
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", CertificateVersion = "bad-version", File = destination.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessAsync_BlankDestination_ThrowsArgumentException(string file)
    {
        var command = new DownloadCertificateCommand { CertificateName = "test", File = file };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Specify a destination file.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedEncoding_ThrowsArgumentException()
    {
        using TempFile destination = TempFile.ReservePath(".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path, Encoding = "PFX" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate encoding must be PEM or DER.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_ExistingDestination_ThrowsIOException()
    {
        using TempFile destination = TempFile.CreateEmpty();
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(ExistsMessage, Assert.IsType<IOException>(exception).Message);
    }

    private (string Thumbprint, bool HasPrivateKey) ReadDownloadedCertificate(string path, string encoding)
    {
        using X509Certificate2 downloaded = encoding == "DER"
            ? X509CertificateLoader.LoadCertificateFromFile(path)
            : X509Certificate2.CreateFromPem(File.ReadAllText(path));
        return (downloaded.Thumbprint, downloaded.HasPrivateKey);
    }
}