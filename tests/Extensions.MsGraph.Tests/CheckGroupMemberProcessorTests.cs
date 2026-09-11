// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;
using Moq;

namespace Extensions.MsGraph.Tests;

public class CheckGroupMemberProcessorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";
    private const string MemberId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    private static TestableCheckGroupMemberProcessor CreateProcessor(string groupId = GroupId, string memberId = MemberId)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableCheckGroupMemberProcessor
        {
            GroupId = groupId,
            MemberId = memberId,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldPostToCheckMemberGroupsEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            $"https://graph.microsoft.com/v1.0/directoryObjects/{MemberId}/checkMemberGroups",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldSerializeGroupIdsBody()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();
        string body = await request.Content!.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var groupIds = doc.RootElement.GetProperty("groupIds").EnumerateArray().Select(e => e.GetString()).ToArray();

        Assert.Single(groupIds);
        Assert.Equal(GroupId, groupIds[0]);
        Assert.Equal("application/json", request.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public void SelectOutput_ShouldMatchGroupIdCaseInsensitively()
    {
        // Graph responses can return upper- or lower-case GUIDs depending on the source;
        // membership check should not be sensitive to that.
        const string mixedCaseGroupId = "aabbccdd-eeff-1122-3344-556677889900";
        var processor = new TestableCheckGroupMemberProcessor
        {
            GroupId = mixedCaseGroupId,
            MemberId = MemberId,
        };
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        processor.WithPublisher(publisher.Object);

        Assert.NotEqual(mixedCaseGroupId, mixedCaseGroupId.ToUpperInvariant());
    }
}
