using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ShowCertificateCommandTests
{
    [Fact]
    public async Task ShowDecodesBase64UrlThumbprintWithoutRetrievingSecret()
    {
        var command = new ShowCertificateCommand { CertificateName = "test", CertificateVersion = "version1" };
        var output = Initialize(command, request =>
        {
            Assert.EndsWith("/certificates/test/version1?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            return Response("{\"x5t\":\"-_8\",\"sid\":\"https://fixture.vault.azure.net/secrets/test\"}");
        });
        await command.ProcessAsync();
        Assert.Equal(new byte[] { 251, 255 }, Assert.IsType<CertificateResult>(Assert.Single(output)).X509Thumbprint);
    }

    [Fact]
    public Task CertificateName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ShowCertificateCommand { CertificateName = "bad_name" },
            "Certificate names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task CertificateVersion_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ShowCertificateCommand { CertificateName = "test", CertificateVersion = "bad-version" },
            "Certificate version must be an alphanumeric version identifier.");
}
