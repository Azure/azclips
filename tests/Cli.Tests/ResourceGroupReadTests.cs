using System.Net;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

public class ResourceGroupReadTests
{
    [Fact]
    public async Task Get_UsesResourceGroupUri_AndDeserializesResponse()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, """
            {"id":"/subscriptions/sub/resourceGroups/test-rg","name":"test-rg","location":"westus2","tags":{"env":"scenario"},"properties":{"provisioningState":"Succeeded"}}
            """));
        var processor = Configure(new GetResourceGroupProcessor(), pipeline, output, processor =>
        {
            processor.SubscriptionId = SubscriptionId;
            processor.Name = "test-rg";
        });

        await processor.ProcessAsync();

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/test-rg?api-version=2025-04-01", request.Uri);
        Assert.Null(request.Body);
        var group = Assert.IsType<ResourceGroup>(Assert.Single(output.WrittenValues));
        Assert.Equal("test-rg", group.Name);
        Assert.Equal("westus2", group.Location);
        Assert.Equal("scenario", group.Tags?["env"]);
        Assert.Equal("Succeeded", group.Properties?.ProvisioningState);
    }

    [Fact]
    public async Task Get_MissingResourceGroup_PropagatesNotFound_WithoutOutput()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NotFound,
            """{"error":{"code":"ResourceGroupNotFound","message":"Resource group 'missing-rg' could not be found."}}"""));
        var processor = Configure(new GetResourceGroupProcessor(), pipeline, output, processor =>
        {
            processor.SubscriptionId = SubscriptionId;
            processor.Name = "missing-rg";
        });

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Single(sent);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_FollowsNextLink_AndCombinesPages(bool emptyFirstPage)
    {
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version=2025-04-01";
        string nextLink = uri + "&$skiptoken=next-page";
        var (pipeline, output, sent) = Fake(request => Status(HttpStatusCode.OK,
            request.RequestUri!.AbsoluteUri == nextLink
                ? """{"value":[{"name":"second-rg","location":"eastus"}]}"""
                : $$"""{"value":{{(emptyFirstPage ? "[]" : "[{\"name\":\"first-rg\",\"location\":\"westus2\"}]")}},"nextLink":"{{nextLink}}"}"""));
        var processor = Configure(new ListResourceGroupProcessor(), pipeline, output,
            processor => processor.SubscriptionId = SubscriptionId);

        await processor.ProcessAsync();

        Assert.Equal([uri, nextLink], sent.Select(request => request.Uri));
        Assert.All(sent, request => Assert.Equal(HttpMethod.Get, request.Method));
        var groups = Assert.IsType<List<ResourceGroup>>(Assert.Single(output.WrittenValues));
        Assert.Equal(emptyFirstPage ? ["second-rg"] : ["first-rg", "second-rg"], groups.Select(group => group.Name));
        Assert.Equal("eastus", groups.Last().Location);
    }

    [Fact]
    public async Task List_EmptyResponse_WritesEmptyCollection()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, """{"value":[]}"""));
        var processor = Configure(new ListResourceGroupProcessor(), pipeline, output,
            processor => processor.SubscriptionId = SubscriptionId);

        await processor.ProcessAsync();

        Assert.Single(sent);
        Assert.Empty(Assert.IsType<List<ResourceGroup>>(Assert.Single(output.WrittenValues)));
    }

    [Fact]
    public async Task List_LaterPageFailure_PropagatesError_WithoutPartialOutput()
    {
        string nextLink = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version=2025-04-01&$skiptoken=next-page";
        var (pipeline, output, sent) = Fake(request => request.RequestUri!.AbsoluteUri == nextLink
            ? Status(HttpStatusCode.Forbidden, """{"error":{"code":"AuthorizationFailed","message":"Access denied."}}""")
            : Status(HttpStatusCode.OK, $$"""{"value":[{"name":"first-rg"}],"nextLink":"{{nextLink}}"}"""));
        var processor = Configure(new ListResourceGroupProcessor(), pipeline, output,
            processor => processor.SubscriptionId = SubscriptionId);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal(2, sent.Count);
        Assert.Empty(output.WrittenValues);
    }
}