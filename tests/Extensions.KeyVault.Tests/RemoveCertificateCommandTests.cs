using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RemoveCertificateCommandTests
{
    [Fact]
    public async Task DeleteRequiresConfirmationAndReturnsRecoveryMetadata()
    {
        var command = new RemoveCertificateCommand { CertificateName = "test" };
        int calls = 0;
        var output = Initialize(command, request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Delete, request.Method);
            return Response("{\"recoveryId\":\"recovery\",\"deletedDate\":123}");
        });
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.Equal(0, calls);
        command.Yes = true;
        await command.ProcessAsync();
        Assert.Equal("recovery", Assert.IsType<DeletedCertificateResult>(Assert.Single(output)).RecoveryId);
        Assert.Equal(1, calls);
    }

    [Fact]
    public Task CertificateName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new RemoveCertificateCommand { CertificateName = "bad_name", Yes = true },
            "Certificate names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task WithoutConfirmation_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(new RemoveCertificateCommand { CertificateName = "test" },
            "Deletion affects all certificate versions. Confirm with --yes or PowerShell ShouldProcess.");
}
