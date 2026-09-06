// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class GetUserProcessorTests
{
    private static TestableGetUserProcessor CreateProcessor(
        string id,
        string[]? select = null,
        string[]? expand = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableGetUserProcessor
        {
            Id = id,
            Select = select,
            Expand = expand,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldTargetGraphV1UsersEndpoint()
    {
        var processor = CreateProcessor("48d31887-5fad-4d73-a9f5-3c356e68a038");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/48d31887-5fad-4d73-a9f5-3c356e68a038",
            request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldUrlEncodeUpn()
    {
        var processor = CreateProcessor("alice@contoso.com");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/alice%40contoso.com",
            request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldEncodeGuestExtUpn()
    {
        var processor = CreateProcessor("guestuser_fabrikam.com#EXT#@contoso.onmicrosoft.com");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains("%23EXT%23", request.RequestUri!.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("#", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelect_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            id: "48d31887-5fad-4d73-a9f5-3c356e68a038",
            select: new[] { "id", "displayName", "mail" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/48d31887-5fad-4d73-a9f5-3c356e68a038?$select=id%2CdisplayName%2Cmail",
            request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_WithExpand_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            id: "48d31887-5fad-4d73-a9f5-3c356e68a038",
            expand: new[] { "memberOf" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.EndsWith("?$expand=memberOf", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelectAndExpand_ShouldCombineParts()
    {
        var processor = CreateProcessor(
            id: "48d31887-5fad-4d73-a9f5-3c356e68a038",
            select: new[] { "id", "displayName" },
            expand: new[] { "memberOf" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        var uri = request.RequestUri!.OriginalString;
        Assert.Contains("$select=id%2CdisplayName", uri, StringComparison.Ordinal);
        Assert.Contains("$expand=memberOf", uri, StringComparison.Ordinal);
        Assert.Equal(1, uri.Count(static c => c == '&'));
    }
}
