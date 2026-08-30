// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class DeleteApplicationProcessorTests
{
    private static TestableDeleteApplicationProcessor CreateProcessor(string id)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableDeleteApplicationProcessor { Id = id }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldDeleteGraphV1ApplicationsEndpoint()
    {
        var processor = CreateProcessor("48d31887-5fad-4d73-a9f5-3c356e68a038");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/applications/48d31887-5fad-4d73-a9f5-3c356e68a038",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldUrlEncodeId()
    {
        var processor = CreateProcessor("app id/with space");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/applications/app%20id%2Fwith%20space",
            request.RequestUri!.AbsoluteUri);
    }
}
