// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class GetSignedInUserProcessorTests
{
    private static TestableGetSignedInUserProcessor CreateProcessor(
        string[]? select = null,
        string[]? expand = null,
        bool appendSelected = false)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableGetSignedInUserProcessor
        {
            Select = select,
            Expand = expand,
            AppendSelected = appendSelected,
        }.WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldTargetGraphMeEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/me", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_WithSelect_ShouldAppendQuery()
    {
        var processor = CreateProcessor(select: new[] { "id", "displayName", "mail" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/me?$select=id%2CdisplayName%2Cmail",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_WithAppendSelected_ShouldInjectDefaults()
    {
        var processor = CreateProcessor(select: new[] { "city" }, appendSelected: true);

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        var uri = request.RequestUri!.ToString();
        Assert.Contains(Uri.EscapeDataString("displayName,id,deletedDateTime,userPrincipalName,usageLocation,givenName,surname,accountEnabled,mailNickname,mail,city"), uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithExpand_ShouldAppendQuery()
    {
        var processor = CreateProcessor(expand: new[] { "memberOf" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.EndsWith("?$expand=memberOf", request.RequestUri!.ToString(), StringComparison.Ordinal);
    }
}
