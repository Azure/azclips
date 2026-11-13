using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ListCertificatesCommandTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task ListsPreserveInheritedPageSizeAndEnumeratePages(int? pageSize)
    {
        var command = new ListCertificatesCommand { Maxresults = pageSize, IncludePending = false };
        int calls = 0;
        var output = Initialize(command, request =>
        {
            if (++calls == 1)
            {
                Assert.Equal(pageSize.HasValue, request.RequestUri!.Query.Contains("maxresults="));
                Assert.Contains("includePending=false", request.RequestUri.Query);
                return Response("{\"value\":[{\"id\":\"one\"}],\"nextLink\":\"https://fixture.vault.azure.net/certificates?skiptoken=next\"}");
            }
            return Response("{\"value\":[{\"id\":\"two\"}]}");
        });
        await command.ProcessAsync();
        Assert.Equal(2, calls);
        Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<CertificateMetadata>>(Assert.Single(output)).Count());
    }

    [Fact]
    public Task PageSize_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new ListCertificatesCommand { PageSize = 0 },
            "Page size must be between 1 and 25.");
}
