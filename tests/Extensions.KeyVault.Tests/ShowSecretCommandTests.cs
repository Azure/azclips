using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ShowSecretCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowRequiresOptInToReturnPlaintext(bool plaintext)
    {
        var command = new ShowSecretCommand { SecretName = "test", SecretVersion = "version1", AsPlainText = plaintext };
        var output = Initialize(command, "{\"value\":\"private-fixture\"}", request =>
        {
            Assert.EndsWith("/secrets/test/version1?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            return Task.CompletedTask;
        });
        await command.ProcessAsync();
        if (plaintext) Assert.Equal("private-fixture", Assert.Single(output));
        else Assert.IsType<SecretMetadata>(Assert.Single(output));
    }

    [Fact]
    public Task SecretName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ShowSecretCommand { SecretName = "bad_name" },
            "Secret names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task SecretVersion_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ShowSecretCommand { SecretName = "test", SecretVersion = "bad-version" },
            "Secret version must be an alphanumeric version identifier.");
}
