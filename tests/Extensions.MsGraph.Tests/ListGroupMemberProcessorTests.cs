// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class ListGroupMemberProcessorTests
{
    private static TestableListGroupMemberProcessor CreateProcessor(
        string? group = null,
        string? groupDisplayName = null,
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
            Group = group,
            GroupDisplayName = groupDisplayName,
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
        var processor = CreateProcessor(group: "11111111-2222-3333-4444-555555555555");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/groups/11111111-2222-3333-4444-555555555555/members",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_MissingGroup_ShouldThrow()
    {
        var processor = CreateProcessor();

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }

    [Fact]
    public async Task CreateRequestMessage_WithFilter_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            group: "11111111-2222-3333-4444-555555555555",
            filter: "startswith(displayName,'Ada')");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.StartsWith(
            "https://graph.microsoft.com/v1.0/groups/11111111-2222-3333-4444-555555555555/members?$filter=",
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
            group: "11111111-2222-3333-4444-555555555555",
            search: "\"displayName:ada\"",
            consistencyLevel: "eventual");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.True(request.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", values!.Single());
    }
}
