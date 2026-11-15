using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RemoveSecretCommandTests
{
    [Fact]
    public async Task DeleteRequiresConfirmationAndPreservesRecoveryMetadata()
    {
        var command = new RemoveSecretCommand { SecretName = "test" };
        int requests = 0;
        var output = Initialize(command, "{\"value\":\"private-fixture\",\"recoveryId\":\"recovery\",\"deletedDate\":123}", request =>
        {
            requests++;
            Assert.Equal(HttpMethod.Delete, request.Method);
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.Equal(0, requests);
        command.Yes = true;
        await command.ProcessAsync();
        Assert.Equal("recovery", Assert.IsType<SecretMetadata>(Assert.Single(output)).RecoveryId);
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Fact]
    public Task SecretName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new RemoveSecretCommand { SecretName = "bad_name", Yes = true },
            "Secret names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task WithoutConfirmation_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(new RemoveSecretCommand { SecretName = "test" },
            "Deletion affects all secret versions. Confirm with --yes or PowerShell ShouldProcess.");
}
