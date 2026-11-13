using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ListSecretsCommandTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task ListsRespectInheritedPageSize(int? pageSize)
    {
        var command = new ListSecretsCommand { Maxresults = pageSize };
        Initialize(command, "{\"value\":[]}", request =>
        {
            Assert.Equal(pageSize.HasValue, request.RequestUri!.Query.Contains("maxresults="));
            if (pageSize.HasValue) Assert.Contains("maxresults=1", request.RequestUri.Query);
            return Task.CompletedTask;
        });
        await command.ProcessAsync();
    }

    [Fact]
    public Task PageSize_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ListSecretsCommand { PageSize = 0 },
            "Page size must be between 1 and 25.");
}
