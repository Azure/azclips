using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ListSecretVersionsCommandTests
{
    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_NoPageSize_OmitsMaxResults()
    {
        var command = new ListSecretVersionsCommand { SecretName = "test" };
        _mockPipeline.RespondWith("{\"value\":[]}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["GET https://fixture.vault.azure.net/secrets/test/versions?api-version=2025-07-01"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_InheritedPageSize_SendsMaxResults()
    {
        var command = new ListSecretVersionsCommand { SecretName = "test", Maxresults = 1 };
        _mockPipeline.RespondWith("{\"value\":[]}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Contains("maxresults=1", _mockPipeline.SentRequests().Single().RequestUri!.Query);
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretName_ThrowsArgumentException()
    {
        var command = new ListSecretVersionsCommand { SecretName = "bad_name" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PageSizeOutOfRange_ThrowsArgumentException()
    {
        var command = new ListSecretVersionsCommand { SecretName = "test", PageSize = 26 };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Page size must be between 1 and 25.", Assert.IsType<ArgumentException>(exception).Message);
    }
}