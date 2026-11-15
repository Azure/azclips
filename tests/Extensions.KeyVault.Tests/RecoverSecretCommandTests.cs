using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RecoverSecretCommandTests
{
    [Fact]
    public async Task RecoverSendsBodylessPostAndReturnsOnlyMetadata()
    {
        var command = new RecoverSecretCommand { SecretName = "test" };
        int requests = 0;
        var output = Initialize(command,
            "{\"id\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"value\":\"private-fixture\",\"attributes\":{\"enabled\":false}}",
            request =>
            {
                requests++;
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://fixture.vault.azure.net/deletedsecrets/test/recover?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
                Assert.Null(request.Content);
                return Task.CompletedTask;
            });

        await command.ProcessAsync();

        Assert.Equal(1, requests);
        var metadata = Assert.IsType<SecretMetadata>(Assert.Single(output));
        Assert.Equal("https://fixture.vault.azure.net/secrets/test/version1", metadata.Id);
        Assert.False(metadata.Attributes!.Enabled);
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task RecoverRejectsInvalidNamesBeforeNetwork(string name)
    {
        var command = new RecoverSecretCommand { SecretName = name };
        var output = Initialize(command, "{}", _ => throw new InvalidOperationException("Unexpected request."));

        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());

        Assert.Empty(output);
    }
}
