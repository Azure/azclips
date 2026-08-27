// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class ListGroupMemberProcessorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";

    private static TestableListGroupMemberProcessor CreateProcessor(
        string groupId = GroupId,
        string? filter = null,
        string[]? select = null,
        string[]? expand = null,
        string[]? orderby = null,
        string? search = null,
        string? consistencyLevel = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableListGroupMemberProcessor
        {
            GroupId = groupId,
            Filter = filter,
            Select = select,
            Expand = expand,
            Orderby = orderby,
            Search = search,
            ConsistencyLevel = consistencyLevel,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldTargetGroupMembersEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            $"https://graph.microsoft.com/v1.0/groups/{GroupId}/members",
            request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRequestMessage_MissingGroupId_ShouldThrow(string groupId)
    {
        var processor = CreateProcessor(groupId: groupId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }

    [Fact]
    public async Task CreateRequestMessage_WithFilter_ShouldAppendQuery()
    {
        var processor = CreateProcessor(filter: "startswith(displayName,'Ada')");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.StartsWith(
            $"https://graph.microsoft.com/v1.0/groups/{GroupId}/members?$filter=",
            request.RequestUri!.OriginalString,
            StringComparison.Ordinal);
        Assert.Contains(
            Uri.EscapeDataString("startswith(displayName,'Ada')"),
            request.RequestUri!.OriginalString,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ConsistencyLevel_ShouldAppearAsHeader()
    {
        var processor = CreateProcessor(
            search: "\"displayName:ada\"",
            consistencyLevel: "eventual");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.True(request.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", values!.Single());
    }
}
