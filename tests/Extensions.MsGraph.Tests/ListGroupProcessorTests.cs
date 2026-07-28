// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class ListGroupProcessorTests
{
    private static TestableListGroupProcessor CreateProcessor(
        string? filter = null,
        string? displayName = null,
        string? displayNamePrefix = null,
        string[]? select = null,
        string[]? expand = null,
        string[]? orderby = null,
        string? search = null,
        string? consistencyLevel = null,
        bool appendSelected = false)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableListGroupProcessor
        {
            Filter = filter,
            DisplayName = displayName,
            DisplayNamePrefix = displayNamePrefix,
            Select = select,
            Expand = expand,
            Orderby = orderby,
            Search = search,
            ConsistencyLevel = consistencyLevel,
            AppendSelected = appendSelected,
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
    public async Task CreateRequestMessage_WithDisplayName_ShouldEmitEqFilter()
    {
        var processor = CreateProcessor(displayName: "Sales Team");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains($"$filter={Uri.EscapeDataString("displayName eq 'Sales Team'")}", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithDisplayNamePrefix_ShouldEmitStartswithFilter()
    {
        var processor = CreateProcessor(displayNamePrefix: "Sal");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains($"$filter={Uri.EscapeDataString("startswith(displayName,'Sal')")}", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_WithFilterAndPrefix_ShouldAndCompose()
    {
        var processor = CreateProcessor(
            filter: "securityEnabled eq true",
            displayNamePrefix: "Eng");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        var expected = "securityEnabled eq true and startswith(displayName,'Eng')";
        Assert.Contains($"$filter={Uri.EscapeDataString(expected)}", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_EscapeSingleQuotesInShortcuts()
    {
        var processor = CreateProcessor(displayName: "O'Brien");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        // OData literal escaping: single quotes doubled.
        Assert.Contains(Uri.EscapeDataString("displayName eq 'O''Brien'"), request.RequestUri!.OriginalString, StringComparison.Ordinal);
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

    [Fact]
    public async Task CreateRequestMessage_WithAppendSelected_ShouldInjectGroupDefaults()
    {
        var processor = CreateProcessor(
            select: new[] { "visibility" },
            appendSelected: true);

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains(Uri.EscapeDataString("displayName,id,deletedDateTime,securityEnabled,mailEnabled,mailNickname,description,visibility"), request.RequestUri!.ToString(), StringComparison.Ordinal);
    }
}
