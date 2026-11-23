using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class RemoveSecretCommandTests
{
    private const string DeletedSecret = "{\"value\":\"private-fixture\",\"recoveryId\":\"recovery\",\"deletedDate\":123}";
    private const string ConfirmationMessage = "Deletion affects all secret versions. Confirm with --yes or PowerShell ShouldProcess.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_Confirmed_SendsDeleteRequest()
    {
        var command = new RemoveSecretCommand { SecretName = "test", Yes = true };
        _mockPipeline.RespondWith(DeletedSecret);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([HttpMethod.Delete], _mockPipeline.SentRequests().Select(r => r.Method));
    }

    [Fact]
    public async Task ProcessAsync_Confirmed_WritesRecoveryId()
    {
        var command = new RemoveSecretCommand { SecretName = "test", Yes = true };
        _mockPipeline.RespondWith(DeletedSecret);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["recovery"], output.Cast<SecretMetadata>().Select(r => r.RecoveryId));
    }

    [Fact]
    public async Task ProcessAsync_ResponseWithValue_OmitsValueFromOutput()
    {
        var command = new RemoveSecretCommand { SecretName = "test", Yes = true };
        _mockPipeline.RespondWith(DeletedSecret);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretName_ThrowsArgumentException()
    {
        var command = new RemoveSecretCommand { SecretName = "bad_name", Yes = true };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_WithoutConfirmation_ThrowsArgumentException()
    {
        var command = new RemoveSecretCommand { SecretName = "test" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(ConfirmationMessage, Assert.IsType<ArgumentException>(exception).Message);
    }
}