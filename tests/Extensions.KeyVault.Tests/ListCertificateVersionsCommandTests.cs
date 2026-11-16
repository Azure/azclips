using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class ListCertificateVersionsCommandTests
{
    private const string FirstPage = "{\"value\":[{\"id\":\"one\"}],\"nextLink\":\"https://fixture.vault.azure.net/certificates?skiptoken=next\"}";
    private const string LastPage = "{\"value\":[{\"id\":\"two\"}]}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_NoPageSize_OmitsMaxResults()
    {
        var command = new ListCertificateVersionsCommand { CertificateName = "test" };
        _mockPipeline.RespondWith(FirstPage, LastPage);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.DoesNotContain("maxresults=", _mockPipeline.SentRequests()[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ProcessAsync_InheritedPageSize_SendsMaxResults()
    {
        var command = new ListCertificateVersionsCommand { CertificateName = "test", Maxresults = 1 };
        _mockPipeline.RespondWith(FirstPage, LastPage);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Contains("maxresults=1", _mockPipeline.SentRequests()[0].RequestUri!.Query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task ProcessAsync_NextLink_RequestsNextPage(int? pageSize)
    {
        var command = new ListCertificateVersionsCommand { CertificateName = "test", Maxresults = pageSize };
        _mockPipeline.RespondWith(FirstPage, LastPage);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(2, _mockPipeline.SentRequests().Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task ProcessAsync_NextLink_WritesItemsFromAllPages(int? pageSize)
    {
        var command = new ListCertificateVersionsCommand { CertificateName = "test", Maxresults = pageSize };
        _mockPipeline.RespondWith(FirstPage, LastPage);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([2], output.Cast<IEnumerable<CertificateMetadata>>().Select(page => page.Count()));
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateName_ThrowsArgumentException()
    {
        var command = new ListCertificateVersionsCommand { CertificateName = "bad_name" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_PageSizeOutOfRange_ThrowsArgumentException()
    {
        var command = new ListCertificateVersionsCommand { CertificateName = "test", PageSize = 26 };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Page size must be between 1 and 25.", Assert.IsType<ArgumentException>(exception).Message);
    }
}