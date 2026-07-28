// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class GetGroupProcessorTests
{
    private static TestableGetGroupProcessor CreateProcessor(
        string group,
        string[]? select = null,
        string[]? expand = null,
        bool appendSelected = false)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableGetGroupProcessor
        {
            Group = group,
            Select = select,
            Expand = expand,
            AppendSelected = appendSelected,
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
    public async Task CreateRequestMessage_MissingGroup_ShouldThrow(string group)
    {
        var processor = CreateProcessor(group);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelect_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            group: "11111111-2222-3333-4444-555555555555",
            select: new[] { "id", "displayName", "mailNickname" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/groups/11111111-2222-3333-4444-555555555555?$select=id%2CdisplayName%2CmailNickname",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_WithAppendSelected_ShouldInjectGroupDefaults()
    {
        var processor = CreateProcessor(
            group: "11111111-2222-3333-4444-555555555555",
            select: new[] { "visibility" },
            appendSelected: true);

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        // Group defaults are distinct from user defaults; guards against sharing the wrong set.
        var uri = request.RequestUri!.ToString();
        Assert.Contains("$select=", uri, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("displayName,id,deletedDateTime,securityEnabled,mailEnabled,mailNickname,description,visibility"), uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithExpand_ShouldAppendQuery()
    {
        var processor = CreateProcessor(
            group: "11111111-2222-3333-4444-555555555555",
            expand: new[] { "members" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.EndsWith("?$expand=members", request.RequestUri!.ToString(), StringComparison.Ordinal);
    }
}
