using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ListSecretsCommandTests
{
    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_NoPageSize_OmitsMaxResults()
    {
        var command = new ListSecretsCommand();
        _mockPipeline.RespondWith("{\"value\":[]}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["GET https://fixture.vault.azure.net/secrets?api-version=2025-07-01"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_InheritedPageSize_SendsMaxResults()
    {
        var command = new ListSecretsCommand { Maxresults = 1 };
        _mockPipeline.RespondWith("{\"value\":[]}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Contains("maxresults=1", _mockPipeline.SentRequests().Single().RequestUri!.Query);
    }

    [Fact]
    public async Task ProcessAsync_PageSizeOutOfRange_ThrowsArgumentException()
    {
        var command = new ListSecretsCommand { PageSize = 0 };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Page size must be between 1 and 25.", Assert.IsType<ArgumentException>(exception).Message);
    }
}