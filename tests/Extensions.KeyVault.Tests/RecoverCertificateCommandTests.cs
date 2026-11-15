using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RecoverCertificateCommandTests
{
    [Fact]
    public async Task RecoverSendsBodylessPostAndReturnsPublicCertificateWithoutFetchingSecret()
    {
        var command = new RecoverCertificateCommand { CertificateName = "test" };
        int calls = 0;
        var output = Initialize(command, request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://fixture.vault.azure.net/deletedcertificates/test/recover?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Content);
            return Response("{\"id\":\"https://fixture.vault.azure.net/certificates/test/version1\",\"sid\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"cer\":\"AQID\",\"x5t\":\"-_8\",\"attributes\":{\"enabled\":false},\"value\":\"private-fixture\",\"pwd\":\"private-fixture\"}");
        });

        await command.ProcessAsync();

        Assert.Equal(1, calls);
        var certificate = Assert.IsType<CertificateResult>(Assert.Single(output));
        Assert.Equal("https://fixture.vault.azure.net/certificates/test/version1", certificate.Id);
        Assert.Equal(new byte[] { 1, 2, 3 }, certificate.Cer);
        Assert.Equal(new byte[] { 251, 255 }, certificate.X509Thumbprint);
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task RecoverRejectsInvalidNamesBeforeNetwork(string name)
    {
        var command = new RecoverCertificateCommand { CertificateName = name };
        var output = Initialize(command, _ => throw new InvalidOperationException("Unexpected request."));

        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());

        Assert.Empty(output);
    }
}
