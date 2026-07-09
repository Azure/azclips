// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.Graph.Tests;

public class GetUserProcessorTests
{
    private static TestableGetUserProcessor CreateProcessor(string id) =>
        new TestableGetUserProcessor { Id = id }.WithPublisher(Mock.Of<IEventPublisher>());

    [Fact]
    public async Task CreateRequestMessage_ShouldTargetGraphV1UsersEndpoint()
    {
        var processor = CreateProcessor("48d31887-5fad-4d73-a9f5-3c356e68a038");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/48d31887-5fad-4d73-a9f5-3c356e68a038",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldUrlEncodeUpn()
    {
        var processor = CreateProcessor("alice@contoso.com");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        // '@' must be percent-encoded so UPNs are safe in the path segment.
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/alice%40contoso.com",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldEncodeGuestExtUpn()
    {
        var processor = CreateProcessor("guestuser_fabrikam.com#EXT#@contoso.onmicrosoft.com");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains("%23EXT%23", request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("#", request.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRequestMessage_MissingId_ShouldThrow(string id)
    {
        var processor = CreateProcessor(id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }
}
