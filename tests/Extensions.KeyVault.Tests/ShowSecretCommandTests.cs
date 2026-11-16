using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ShowSecretCommandTests
{
    private const string Secret = "{\"value\":\"private-fixture\"}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_Version_SendsVersionedSecretRequest(bool plaintext)
    {
        var command = new ShowSecretCommand { SecretName = "test", SecretVersion = "version1", AsPlainText = plaintext };
        _mockPipeline.RespondWith(Secret);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["GET https://fixture.vault.azure.net/secrets/test/version1?api-version=2025-07-01"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_AsPlainText_WritesSecretValue()
    {
        var command = new ShowSecretCommand { SecretName = "test", SecretVersion = "version1", AsPlainText = true };
        _mockPipeline.RespondWith(Secret);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["private-fixture"], output);
    }

    [Fact]
    public async Task ProcessAsync_WithoutAsPlainText_WritesMetadata()
    {
        var command = new ShowSecretCommand { SecretName = "test", SecretVersion = "version1", AsPlainText = false };
        _mockPipeline.RespondWith(Secret);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(output.Single());
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretName_ThrowsArgumentException()
    {
        var command = new ShowSecretCommand { SecretName = "bad_name" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretVersion_ThrowsArgumentException()
    {
        var command = new ShowSecretCommand { SecretName = "test", SecretVersion = "bad-version" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }
}