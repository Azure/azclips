// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;
using Moq;

namespace Extensions.Graph.Tests;

public class CheckGroupMemberProcessorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";
    private const string MemberId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    private static TestableCheckGroupMemberProcessor CreateProcessor(string group = GroupId, string memberId = MemberId)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableCheckGroupMemberProcessor
        {
            Group = group,
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

    [Theory]
    [InlineData("", MemberId)]
    [InlineData("   ", MemberId)]
    [InlineData(GroupId, "")]
    [InlineData(GroupId, "   ")]
    public async Task CreateRequestMessage_MissingRequiredParam_ShouldThrow(string group, string memberId)
    {
        var processor = CreateProcessor(group, memberId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }

    [Fact]
    public async Task SelectOutput_WhenGroupInResponse_ShouldReturnTrue()
    {
        var processor = CreateProcessor();
        // Run request build so the resolver stores the resolved group id used by SelectOutput.
        _ = await processor.BuildRequestAsync();

        bool result = processor.ProjectOutput(new CheckMemberGroupsResponse
        {
            Value = new[] { GroupId, "another-group-id" },
        });

        Assert.True(result);
    }

    [Fact]
    public async Task SelectOutput_WhenGroupNotInResponse_ShouldReturnFalse()
    {
        var processor = CreateProcessor();
        _ = await processor.BuildRequestAsync();

        bool result = processor.ProjectOutput(new CheckMemberGroupsResponse
        {
            Value = new[] { "some-other-group" },
        });

        Assert.False(result);
    }

    [Fact]
    public async Task SelectOutput_WhenResponseValueNull_ShouldReturnFalse()
    {
        var processor = CreateProcessor();
        _ = await processor.BuildRequestAsync();

        bool result = processor.ProjectOutput(new CheckMemberGroupsResponse { Value = null });

        Assert.False(result);
    }

    [Fact]
    public async Task SelectOutput_ShouldMatchGroupIdCaseInsensitively()
    {
        // Graph responses can return upper- or lower-case GUIDs depending on the source;
        // membership check should not be sensitive to that.
        const string mixedCaseGroupId = "aabbccdd-eeff-1122-3344-556677889900";
        var processor = new TestableCheckGroupMemberProcessor
        {
            Group = mixedCaseGroupId,
            MemberId = MemberId,
        };
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        processor.WithPublisher(publisher.Object);
        _ = await processor.BuildRequestAsync();

        bool result = processor.ProjectOutput(new CheckMemberGroupsResponse
        {
            Value = new[] { mixedCaseGroupId.ToUpperInvariant() },
        });

        Assert.True(result);
        Assert.NotEqual(mixedCaseGroupId, mixedCaseGroupId.ToUpperInvariant());
    }
}
