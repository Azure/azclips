// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class GetGroupProcessorTests
{
    private static TestableGetGroupProcessor CreateProcessor(
        string id,
        string[]? select = null,
        string[]? expand = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableGetGroupProcessor
        {
            Id = id,
            Select = select,
            Expand = expand,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldTargetGraphV1GroupsEndpoint()
    {
        var processor = CreateProcessor("11111111-2222-3333-4444-555555555555");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/groups/11111111-2222-3333-4444-555555555555",
            request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRequestMessage_MissingId_ShouldThrow(string id)
    {
        var processor = CreateProcessor(id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelect_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            id: "11111111-2222-3333-4444-555555555555",
            select: new[] { "id", "displayName", "mailNickname" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/groups/11111111-2222-3333-4444-555555555555?$select=id%2CdisplayName%2CmailNickname",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_WithExpand_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            id: "11111111-2222-3333-4444-555555555555",
            expand: new[] { "members" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.EndsWith("?$expand=members", request.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelectAndExpand_ShouldCombineParts()
    {
        var processor = CreateProcessor(
            id: "11111111-2222-3333-4444-555555555555",
            select: new[] { "id", "displayName" },
            expand: new[] { "members" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        var uri = request.RequestUri!.ToString();
        Assert.Contains("$select=id%2CdisplayName", uri, StringComparison.Ordinal);
        Assert.Contains("$expand=members", uri, StringComparison.Ordinal);
        Assert.Equal(1, uri.Count(static c => c == '&'));
    }
}
