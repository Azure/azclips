// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class ListGroupProcessorTests
{
    private static TestableListGroupProcessor CreateProcessor(
        string? filter = null,
        string[]? select = null,
        string[]? expand = null,
        string[]? orderby = null,
        string? search = null,
        string? consistencyLevel = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableListGroupProcessor
        {
            Filter = filter,
            Select = select,
            Expand = expand,
            Orderby = orderby,
            Search = search,
            ConsistencyLevel = consistencyLevel,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_NoParams_ShouldTargetGroupsEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/groups", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldUrlEncodeFilterExpression()
    {
        var processor = CreateProcessor(filter: "startswith(displayName,'Sal')");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains($"$filter={Uri.EscapeDataString("startswith(displayName,'Sal')")}", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldPassSelectAsCommaSeparatedQuery()
    {
        var processor = CreateProcessor(select: new[] { "id", "displayName" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/groups?$select=id%2CdisplayName",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ConsistencyLevel_ShouldAppearAsHeader()
    {
        var processor = CreateProcessor(
            search: "\"displayName:contoso\"",
            consistencyLevel: "eventual");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.True(request.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", values!.Single());
    }
}
