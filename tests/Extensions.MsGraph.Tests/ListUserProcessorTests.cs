// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;
using Moq;

namespace Extensions.MsGraph.Tests;

public class ListUserProcessorTests
{
    private static TestableListUserProcessor CreateProcessor(
        string? filter = null,
        string[]? select = null,
        string[]? expand = null,
        string[]? orderby = null,
        string? search = null,
        string? consistencyLevel = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableListUserProcessor
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
    public async Task CreateRequestMessage_NoParameters_ShouldTargetGraphV1UsersEndpoint()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/users", request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldUrlEncodeFilterExpression()
    {
        var processor = CreateProcessor(filter: "startswith(displayName,'Ada')");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        var uri = request.RequestUri!.OriginalString;
        Assert.StartsWith("https://graph.microsoft.com/v1.0/users?$filter=", uri, StringComparison.Ordinal);
        Assert.Contains("startswith%28displayName%2C%27Ada%27%29", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldPassSelectAsCommaSeparatedQuery()
    {
        var processor = CreateProcessor(select: new[] { "id", "displayName", "mail" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users?$select=id%2CdisplayName%2Cmail",
            request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldPassExpandAsCommaSeparatedQuery()
    {
        var processor = CreateProcessor(expand: new[] { "memberOf", "ownedObjects" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users?$expand=memberOf%2CownedObjects",
            request.RequestUri!.OriginalString);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldPassOrderbyAsCommaSeparatedQuery()
    {
        var processor = CreateProcessor(orderby: new[] { "displayName", "mail desc" });

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        // Assert against OriginalString: ToString() decodes some reserved chars back to
        // their unencoded form; OriginalString preserves the exact on-the-wire encoding.
        Assert.Contains("$orderby=displayName%2Cmail%20desc", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldEncodeSearchExpression()
    {
        var processor = CreateProcessor(search: "\"displayName:ada\"");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Contains("$search=%22displayName%3Aada%22", request.RequestUri!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ConsistencyLevel_ShouldBeHeaderNotQuery()
    {
        var processor = CreateProcessor(search: "displayName:ada", consistencyLevel: "eventual");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        // ConsistencyLevel travels as an HTTP header per Graph's contract; a query-string
        // form would 400 on advanced queries.
        Assert.True(request.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", Assert.Single(values!));
        Assert.DoesNotContain("consistency", request.RequestUri!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRequestMessage_NoConsistencyLevel_ShouldOmitHeader()
    {
        var processor = CreateProcessor();

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.False(request.Headers.Contains("ConsistencyLevel"));
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldCombineAllOptionalParameters()
    {
        var processor = CreateProcessor(
            filter: "endswith(mail,'@contoso.com')",
            select: new[] { "id", "mail" },
            expand: new[] { "memberOf" },
            orderby: new[] { "displayName" },
            search: "displayName:ada",
            consistencyLevel: "eventual");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        var uri = request.RequestUri!.OriginalString;
        Assert.Contains("$filter=", uri, StringComparison.Ordinal);
        Assert.Contains("$select=", uri, StringComparison.Ordinal);
        Assert.Contains("$expand=", uri, StringComparison.Ordinal);
        Assert.Contains("$orderby=", uri, StringComparison.Ordinal);
        Assert.Contains("$search=", uri, StringComparison.Ordinal);
        // Five query params → four ampersands; guards against a stray param leaking in.
        Assert.Equal(4, uri.Count(static c => c == '&'));
        Assert.True(request.Headers.Contains("ConsistencyLevel"));
    }

    [Fact]
    public void UserList_ShouldDeserializeGraphEnvelope()
    {
        // Captured shape of a real GET /users response: PascalCase model properties bind to
        // camelCase JSON, and @odata.nextLink maps to NextLink via JsonPropertyName.
        const string body = """
            {
              "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#users",
              "@odata.nextLink": "https://graph.microsoft.com/v1.0/users?$skiptoken=X",
              "value": [
                {
                  "id": "00000000-0000-0000-0000-000000000001",
                  "displayName": "Ada Lovelace",
                  "userPrincipalName": "ada@contoso.com",
                  "mail": "ada@contoso.com",
                  "businessPhones": ["+1 555 0100"]
                },
                {
                  "id": "00000000-0000-0000-0000-000000000002",
                  "displayName": "Alan Turing",
                  "userPrincipalName": "alan@contoso.com",
                  "mail": null,
                  "businessPhones": []
                }
              ]
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var list = JsonSerializer.Deserialize<UserList>(body, options);

        Assert.NotNull(list);
        Assert.Equal("https://graph.microsoft.com/v1.0/users?$skiptoken=X", list!.NextLink);
        Assert.NotNull(list.Value);
        Assert.Equal(2, list.Value!.Length);
        Assert.Equal("Ada Lovelace", list.Value[0].DisplayName);
        Assert.Equal("ada@contoso.com", list.Value[0].UserPrincipalName);
        Assert.Equal(new[] { "+1 555 0100" }, list.Value[0].BusinessPhones!);
        Assert.Null(list.Value[1].Mail);
    }

    [Fact]
    public void UserList_EmptyPage_ShouldDeserializeToEmptyArray()
    {
        const string body = """
            {
              "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#users",
              "value": []
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var list = JsonSerializer.Deserialize<UserList>(body, options);

        Assert.NotNull(list);
        Assert.NotNull(list!.Value);
        Assert.Empty(list.Value!);
        Assert.Null(list.NextLink);
    }
}
