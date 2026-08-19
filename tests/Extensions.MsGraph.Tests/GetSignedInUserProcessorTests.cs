// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class GetSignedInUserProcessorTests
{
    private static TestableGetSignedInUserProcessor CreateProcessor(
        string[]? select = null,
        string[]? expand = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableGetSignedInUserProcessor
        {
            Select = select,
            Expand = expand,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldTargetGraphMeEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/me", request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelect_ShouldAppendQuery()
    {
        var processor = CreateProcessor(select: new[] { "id", "displayName", "mail" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/me?$select=id%2CdisplayName%2Cmail",
            request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_WithExpand_ShouldAppendQuery()
    {
        var processor = CreateProcessor(expand: new[] { "memberOf" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.EndsWith("?$expand=memberOf", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }
}
