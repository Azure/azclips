using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RecoverCertificateCommandTests
{
    private const string RecoveredCertificate = "{\"id\":\"https://fixture.vault.azure.net/certificates/test/version1\",\"sid\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"cer\":\"AQID\",\"x5t\":\"-_8\",\"attributes\":{\"enabled\":false},\"value\":\"private-fixture\",\"pwd\":\"private-fixture\"}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_ValidName_SendsRecoverPost()
    {
        var command = new RecoverCertificateCommand { CertificateName = "test" };
        _mockPipeline.RespondWith(RecoveredCertificate);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["POST https://fixture.vault.azure.net/deletedcertificates/test/recover?api-version=2025-07-01"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_ValidName_SendsNoBody()
    {
        var command = new RecoverCertificateCommand { CertificateName = "test" };
        List<string?> sentBodies = _mockPipeline.RespondWith(RecoveredCertificate);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(sentBodies.Single());
    }

    [Fact]
    public async Task ProcessAsync_ValidName_WritesPublicCertificate()
    {
        var command = new RecoverCertificateCommand { CertificateName = "test" };
        _mockPipeline.RespondWith(RecoveredCertificate);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equivalent(
            new[] { new { Id = "https://fixture.vault.azure.net/certificates/test/version1", Cer = new byte[] { 1, 2, 3 }, X509Thumbprint = new byte[] { 251, 255 } } },
            output.Cast<CertificateResult>());
    }

    [Fact]
    public async Task ProcessAsync_ResponseWithSecretMaterial_OmitsSecretMaterialFromOutput()
    {
        var command = new RecoverCertificateCommand { CertificateName = "test" };
        _mockPipeline.RespondWith(RecoveredCertificate);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task ProcessAsync_InvalidName_ThrowsArgumentException(string name)
    {
        var command = new RecoverCertificateCommand { CertificateName = name };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task ProcessAsync_InvalidName_WritesNoOutput(string name)
    {
        var command = new RecoverCertificateCommand { CertificateName = name };
        var output = Initialize(command, _mockPipeline);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Empty(output);
    }
}