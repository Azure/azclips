// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for the shared <see cref="ArmListPaginator"/> that backs every
/// <c>List*Processor</c>. Exercises envelope unwrapping, empty lists, multi-page nextLink
/// traversal, and the safety guards (cyclic / non-absolute nextLink, truncating page).
/// </summary>
public class ArmListPaginatorTests
{
    private sealed class Item
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class ItemListResult : IArmListPage<Item>
    {
        [JsonPropertyName("value")] public Item[]? Value { get; set; }
        [JsonPropertyName("nextLink")] public string? NextLink { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task CollectAsync(FakeHttpPipeline pipeline, FakeOutputHandler output, Func<Task<HttpResponseMessage>> sendFirst) =>
        ArmListPaginator.CollectAndWriteAsync<ItemListResult, Item>(
            pipeline, new FakeEventPublisher(), Options, output.WriteOutput, sendFirst);

    [Fact]
    public async Task UnwrapsEnvelope_IntoFlatArray()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [ { "name": "a" }, { "name": "b" } ] }"""));

        await CollectAsync(pipeline, output, () => pipeline.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/x"), new FakeEventPublisher()));

        var items = Assert.IsType<Item[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(new[] { "a", "b" }, items.Select(i => i.Name));
    }

    [Fact]
    public async Task EmptyValue_WritesEmptyArray()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [] }"""));

        await CollectAsync(pipeline, output, () => pipeline.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/x"), new FakeEventPublisher()));

        Assert.Empty(Assert.IsType<Item[]>(Assert.Single(output.WrittenValues)));
    }

    [Fact]
    public async Task FollowsNextLink_AcrossPages()
    {
        const string page1 = """{ "value": [ { "name": "a" } ], "nextLink": "https://management.azure.com/x?%24skiptoken=p2" }""";
        const string page2 = """{ "value": [ { "name": "b" } ] }""";
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((req, _) =>
            req.RequestUri!.Query.Contains("skiptoken", StringComparison.OrdinalIgnoreCase) ? Json(page2) : Json(page1));

        await CollectAsync(pipeline, output, () => pipeline.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/x"), new FakeEventPublisher()));

        Assert.Equal(2, pipeline.SendCount);
        var items = Assert.IsType<Item[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(new[] { "a", "b" }, items.Select(i => i.Name));
    }

    [Theory]
    [InlineData("relative/next")]      // relative reference: non-absolute on every platform
    [InlineData("/leading/slash")]     // absolute file: URI on Unix, still not HTTP(S)
    [InlineData("ftp://example.com/x")] // absolute but non-HTTP(S) scheme
    public async Task NonHttpNextLink_Throws(string nextLink)
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json($$"""{ "value": [ { "name": "a" } ], "nextLink": "{{nextLink}}" }"""));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CollectAsync(pipeline, output, () => pipeline.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/x"), new FakeEventPublisher())));

        Assert.Contains("not an absolute HTTP(S) URI", ex.Message);
    }

    [Fact]
    public async Task NextLinkPageWithNoBody_ThrowsToAvoidTruncation()
    {
        // First page advertises a nextLink; the follow-up page returns an empty body. The
        // paginator must fail fast rather than silently truncating the result set.
        const string page1 = """{ "value": [ { "name": "a" } ], "nextLink": "https://management.azure.com/x?%24skiptoken=p2" }""";
        var output = new FakeOutputHandler();
        var call = 0;
        var pipeline = new FakeHttpPipeline((_, _) =>
            ++call == 1
                ? Json(page1)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty, Encoding.UTF8, "application/json") });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CollectAsync(pipeline, output, () => pipeline.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/x"), new FakeEventPublisher())));

        Assert.Contains("would truncate results", ex.Message);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task CyclicNextLink_Throws()
    {
        const string cyclic = """{ "value": [ { "name": "a" } ], "nextLink": "https://management.azure.com/x?%24skiptoken=loop" }""";
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json(cyclic));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CollectAsync(pipeline, output, () => pipeline.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/x"), new FakeEventPublisher())));

        Assert.Contains("cyclic nextLink", ex.Message);
    }
}
