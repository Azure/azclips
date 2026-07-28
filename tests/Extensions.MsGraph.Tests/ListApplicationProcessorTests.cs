// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class ListApplicationProcessorTests
{
    private static TestableListApplicationProcessor CreateProcessor() =>
        new TestableListApplicationProcessor().WithPublisher(Mock.Of<IEventPublisher>());

    [Fact]
    public async Task CreateRequestMessage_NoFilters_ShouldTargetApplicationsEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/applications", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequestMessage_DisplayName_ShouldBuildEqFilter()
    {
        var processor = CreateProcessor();
        processor.DisplayName = "contoso-app";

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        string uri = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/applications?$filter=displayName eq 'contoso-app'",
            uri);
    }

    [Fact]
    public async Task CreateRequestMessage_DisplayName_ShouldEscapeSingleQuotes()
    {
        var processor = CreateProcessor();
        processor.DisplayName = "o'brien";

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        // OData escapes a single quote by doubling it.
        string uri = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
        Assert.Contains("displayName eq 'o''brien'", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_RawFilterAndDisplayName_ShouldCombineWithAnd()
    {
        var processor = CreateProcessor();
        processor.Filter = "signInAudience eq 'AzureADMyOrg'";
        processor.DisplayName = "contoso-app";

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        string uri = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
        Assert.Contains("signInAudience eq 'AzureADMyOrg' and displayName eq 'contoso-app'", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_Select_ShouldEmitSelectQuery()
    {
        var processor = CreateProcessor();
        processor.Select = ["displayName", "appId"];

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains("$select=displayName%2CappId", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ConsistencyLevel_ShouldSetHeader()
    {
        var processor = CreateProcessor();
        processor.ConsistencyLevel = "eventual";

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.True(request.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", Assert.Single(values!));
    }
}
