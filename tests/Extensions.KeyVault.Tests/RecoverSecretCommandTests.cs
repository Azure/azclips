using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RecoverSecretCommandTests
{
    private const string RecoveredSecret = "{\"id\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"value\":\"private-fixture\",\"attributes\":{\"enabled\":false}}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_ValidName_SendsRecoverPost()
    {
        var command = new RecoverSecretCommand { SecretName = "test" };
        _mockPipeline.RespondWith(RecoveredSecret);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["POST https://fixture.vault.azure.net/deletedsecrets/test/recover?api-version=2025-07-01"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_ValidName_SendsNoBody()
    {
        var command = new RecoverSecretCommand { SecretName = "test" };
        List<string?> sentBodies = _mockPipeline.RespondWith(RecoveredSecret);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(sentBodies.Single());
    }

    [Fact]
    public async Task ProcessAsync_ValidName_WritesMetadata()
    {
        var command = new RecoverSecretCommand { SecretName = "test" };
        _mockPipeline.RespondWith(RecoveredSecret);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(
            [("https://fixture.vault.azure.net/secrets/test/version1", (bool?)false)],
            output.Cast<SecretMetadata>().Select(metadata => (metadata.Id, metadata.Attributes!.Enabled)));
    }

    [Fact]
    public async Task ProcessAsync_ResponseWithValue_OmitsValueFromOutput()
    {
        var command = new RecoverSecretCommand { SecretName = "test" };
        _mockPipeline.RespondWith(RecoveredSecret);
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
        var command = new RecoverSecretCommand { SecretName = name };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task ProcessAsync_InvalidName_WritesNoOutput(string name)
    {
        var command = new RecoverSecretCommand { SecretName = name };
        var output = Initialize(command, _mockPipeline);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Empty(output);
    }
}