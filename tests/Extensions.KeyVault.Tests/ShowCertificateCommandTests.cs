using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ShowCertificateCommandTests
{
    private const string Certificate = "{\"x5t\":\"-_8\",\"sid\":\"https://fixture.vault.azure.net/secrets/test\"}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_Version_SendsOnlyVersionedCertificateRequest()
    {
        var command = new ShowCertificateCommand { CertificateName = "test", CertificateVersion = "version1" };
        _mockPipeline.RespondWith(Certificate);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["GET https://fixture.vault.azure.net/certificates/test/version1?api-version=2025-07-01"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_Base64UrlThumbprint_WritesDecodedThumbprint()
    {
        var command = new ShowCertificateCommand { CertificateName = "test", CertificateVersion = "version1" };
        _mockPipeline.RespondWith(Certificate);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(new byte[] { 251, 255 }, output.Cast<CertificateResult>().Single().X509Thumbprint);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateName_ThrowsArgumentException()
    {
        var command = new ShowCertificateCommand { CertificateName = "bad_name" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateVersion_ThrowsArgumentException()
    {
        var command = new ShowCertificateCommand { CertificateName = "test", CertificateVersion = "bad-version" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }
}