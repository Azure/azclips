using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Moq;

namespace Extensions.KeyVault.Tests;

public class PaginationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PagesAreFetchedOnlyWhenTheConsumerAdvances(bool tokenPaging)
    {
        var fixture = new Fixture(tokenPaging);
        IAsyncEnumerable<TestPage> pages = fixture.ReadPages();
        await using var iterator = pages.GetAsyncEnumerator();

        Assert.Empty(fixture.Sent);
        Assert.Empty(fixture.Built);
        Assert.True(await iterator.MoveNextAsync());
        Assert.Equal(["first"], Assert.IsType<string[]>(iterator.Current.Items));
        Assert.Single(fixture.Sent);
        Assert.Empty(fixture.Tokens);
        Assert.All(fixture.Responses, response => Assert.True(response.Disposed));

        Assert.True(await iterator.MoveNextAsync());
        Assert.Equal(["second"], Assert.IsType<string[]>(iterator.Current.Items));
        Assert.Equal(2, fixture.Sent.Count);
        Assert.Equal(tokenPaging ? ["second"] : [], fixture.Tokens);

        Assert.True(await iterator.MoveNextAsync());
        Assert.Equal(["third"], Assert.IsType<string[]>(iterator.Current.Items));
        Assert.False(await iterator.MoveNextAsync());
        Assert.Equal(3, fixture.Sent.Count);
        Assert.Equal(tokenPaging ? ["first", "second", "third"] : ["first"], fixture.Deserialized);
        Assert.All(fixture.Responses, response => Assert.True(response.Disposed));
        Assert.All(fixture.Built, content => Assert.True(content.Disposed));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task StoppingEnumerationDoesNotFetchAnotherPageAndDisposesRequests(bool tokenPaging, int limit)
    {
        var fixture = new Fixture(tokenPaging);
        int count = 0;
        await foreach (TestPage page in fixture.ReadPages())
        {
            if (++count == limit)
            {
                break;
            }
        }

        Assert.Equal(limit, fixture.Sent.Count);
        Assert.Equal(tokenPaging ? limit - 1 : 0, fixture.Tokens.Count);
        Assert.All(fixture.Built, content => Assert.True(content.Disposed));
        Assert.All(fixture.Responses, response => Assert.True(response.Disposed));
    }

    [Fact]
    public async Task TokenPagingPreservesCustomDeserializationForEveryPage()
    {
        var fixture = new Fixture(tokenPaging: true) { Xml = true };

        List<string> items = await fixture.ReadPages().CollectItemsAsync(page => page.Items);

        Assert.Equal(["first", "second", "third"], items);
        Assert.Equal(items, fixture.Deserialized);
        Assert.Equal(3, fixture.Sent.Count);
    }

    [Fact]
    public async Task NextLinkPagingPreservesJsonDeserializationForSubsequentPages()
    {
        var fixture = new Fixture(tokenPaging: false) { Xml = true };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ReadPages().CollectItemsAsync(page => page.Items));

        Assert.Null(error.InnerException);
        Assert.Contains("Raw body: <page", error.Message);
        Assert.Equal(["first"], fixture.Deserialized);
        Assert.Equal(2, fixture.Sent.Count);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("missing-items")]
    [InlineData("empty-items")]
    [InlineData("items")]
    public async Task ExistingNextLinkCollectorStillCreatesAndDisposesItsRequestTemplateWithoutANextPage(string shape)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        TestPage? body = shape == "null" ? null
            : new TestPage(shape == "missing-items" ? null : shape == "empty-items" ? [] : ["first"], null);
        int creates = 0;
        var content = new TrackedContent("request");

        var items = await body.CollectWithNextLinkAsync(page => page.Items, page => page.Next, () =>
        {
            creates++;
            return Task.FromResult(new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/first") { Content = content });
        }, publisher.Object, pipeline.Object, new JsonSerializerOptions());

        Assert.Equal(1, creates);
        Assert.True(content.Disposed);
        Assert.Equal(shape == "items" ? ["first"] : [], items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingCollectorsStillCombineAllPages(bool tokenPaging)
    {
        var fixture = new Fixture(tokenPaging);

        Assert.Equal(["first", "second", "third"], await fixture.CollectLegacy());

        Assert.Equal(3, fixture.Sent.Count);
        Assert.All(fixture.Responses, response => Assert.True(response.Disposed));
        Assert.All(fixture.Built, content => Assert.True(content.Disposed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyPagesWithAContinuationAreNotTreatedAsTheEnd(bool tokenPaging)
    {
        var fixture = new Fixture(tokenPaging) { EmptyFirstPage = true };

        Assert.Equal(["second", "third"], await fixture.ReadPages().CollectItemsAsync(page => page.Items));

        Assert.Equal(3, fixture.Sent.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingItemsStopsCollectionWithoutFetchingAnotherPage(bool tokenPaging)
    {
        var fixture = new Fixture(tokenPaging) { MissingItems = true };

        Assert.Empty(await fixture.ReadPages().CollectItemsAsync(page => page.Items));

        Assert.Single(fixture.Sent);
        Assert.Empty(fixture.Tokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeEnumerationDoesNotSendRequests(bool tokenPaging)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(tokenPaging, cancellation.Token);
        cancellation.Cancel();
        await using var iterator = fixture.ReadPages().GetAsyncEnumerator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => iterator.MoveNextAsync().AsTask());

        Assert.Empty(fixture.Sent);
        Assert.Empty(fixture.Built);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBetweenPagesDoesNotFetchOrSetAnotherToken(bool tokenPaging)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(tokenPaging, cancellation.Token);
        await using (var iterator = fixture.ReadPages().GetAsyncEnumerator())
        {
            Assert.True(await iterator.MoveNextAsync());
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => iterator.MoveNextAsync().AsTask());
        }

        Assert.Single(fixture.Sent);
        Assert.Empty(fixture.Tokens);
        Assert.All(fixture.Built, content => Assert.True(content.Disposed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringTransportIsNotWrappedAsADeserializationError(bool tokenPaging)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(tokenPaging, cancellation.Token);
        await using var iterator = fixture.ReadPages().GetAsyncEnumerator();
        Assert.True(await iterator.MoveNextAsync());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Pipeline.Setup(pipeline => pipeline.SendAsync(It.IsAny<HttpRequestMessage>(), fixture.Publisher))
            .Returns(async (HttpRequestMessage request, IEventPublisher publisher) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, publisher.Token);
                throw new InvalidOperationException("The canceled transport must not complete.");
            });

        Task<bool> next = iterator.MoveNextAsync().AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterHttpFailureDisposesResponsesAndDoesNotReturnPartialCollection(bool tokenPaging)
    {
        var fixture = new Fixture(tokenPaging) { FailSecondPage = true };

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => fixture.ReadPages().CollectItemsAsync(page => page.Items));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal(2, fixture.Sent.Count);
        Assert.All(fixture.Responses, response => Assert.True(response.Disposed));
        Assert.All(fixture.Built, content => Assert.True(content.Disposed));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MalformedLaterPagePropagatesWithSensitiveDiagnosticsRedacted(bool tokenPaging, bool sensitive)
    {
        var fixture = new Fixture(tokenPaging) { InvalidSecondPage = true, Sensitive = sensitive };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ReadPages().CollectItemsAsync(page => page.Items));

        if (sensitive)
        {
            Assert.DoesNotContain("private-page-body", error.ToString());
            Assert.DoesNotContain("fixture.invalid", error.ToString());
        }
        else
        {
            Assert.Contains("private-page-body", error.ToString());
        }
        if (sensitive || !tokenPaging) Assert.Null(error.InnerException);
        else Assert.IsType<JsonException>(error.InnerException);
        string expectedDiagnostic = sensitive
            ? tokenPaging ? "Response could not be deserialized. Sensitive details omitted." : "Response has invalid JSON. Sensitive details omitted."
            : tokenPaging ? "response could not be deserialized for https://fixture.invalid/second"
                : "response has invalid JSON for https://fixture.invalid/second";
        Assert.Contains(Mock.Get(fixture.Publisher).Invocations,
            invocation => invocation.Arguments.OfType<string>().Contains(expectedDiagnostic));
        Assert.Equal(2, fixture.Sent.Count);
        Assert.All(fixture.Responses, response => Assert.True(response.Disposed));
        Assert.All(fixture.Built, content => Assert.True(content.Disposed));
    }

    private sealed record TestPage(string[]? Items, string? Next);

    private sealed class Fixture
    {
        private readonly bool tokenPaging;
        private string? marker;
        public IEventPublisher Publisher { get; }
        public Mock<IRuntimeHttpPipeline> Pipeline { get; } = new();
        public List<string> Sent { get; } = [];
        public List<string> Tokens { get; } = [];
        public List<string> Deserialized { get; } = [];
        public List<TrackedContent> Built { get; } = [];
        public List<TrackedContent> Responses { get; } = [];
        public bool Xml { get; init; }
        public bool MissingItems { get; init; }
        public bool EmptyFirstPage { get; init; }
        public bool FailSecondPage { get; init; }
        public bool InvalidSecondPage { get; init; }
        public bool Sensitive { get; init; }

        public Fixture(bool tokenPaging, CancellationToken token = default)
        {
            this.tokenPaging = tokenPaging;
            var publisher = new Mock<IEventPublisher>();
            publisher.SetReturnsDefault(Task.CompletedTask);
            publisher.SetupGet(value => value.Token).Returns(token);
            Publisher = publisher.Object;
            Pipeline.Setup(pipeline => pipeline.SendAsync(It.IsAny<HttpRequestMessage>(), Publisher))
                .Returns((HttpRequestMessage request, IEventPublisher _) =>
                {
                    Sent.Add(request.RequestUri!.AbsoluteUri);
                    string name = request.RequestUri.Segments.Last();
                    string? next = name switch { "first" => "second", "second" => "third", _ => null };
                    if (next is not null && !tokenPaging) next = $"https://fixture.invalid/{next}";
                    string body = Xml
                        ? new XElement("page", new XAttribute("name", name), next is null ? null : new XAttribute("next", next)).ToString()
                        : JsonSerializer.Serialize(new TestPage(MissingItems ? null : EmptyFirstPage && name == "first" ? [] : [name], next));
                    if (InvalidSecondPage && name == "second") body = "{private-page-body";
                    var content = new TrackedContent(body);
                    Responses.Add(content);
                    return Task.FromResult(new HttpResponseMessage(FailSecondPage && name == "second" ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
                    {
                        RequestMessage = request,
                        Content = content,
                    });
                });
        }

        public IAsyncEnumerable<TestPage> ReadPages() => tokenPaging
            ? Pagination.ReadWithTokenAsync(ReadFirstPage, page => page.Next, SetToken, CreateRequest, Deserialize, Publisher, Pipeline.Object)
            : Pagination.ReadWithNextLinkAsync(ReadFirstPage, page => page.Next, CreateRequest, new JsonSerializerOptions(), Publisher, Pipeline.Object);

        public async Task<List<string>> CollectLegacy()
        {
            TestPage? body = await ReadFirstPage();
            return tokenPaging
                ? await body.CollectWithTokenAsync(page => page.Items, page => page.Next, SetToken,
                    CreateRequest, Deserialize, Publisher, Pipeline.Object)
                : await body.CollectWithNextLinkAsync(page => page.Items, page => page.Next,
                    CreateRequest, Publisher, Pipeline.Object, new JsonSerializerOptions());
        }

        private void SetToken(string token)
        {
            Tokens.Add(token);
            marker = token;
        }

        private Task<HttpRequestMessage> CreateRequest()
        {
            var content = new TrackedContent("request");
            Built.Add(content);
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://fixture.invalid/{marker ?? "first"}")
            {
                Content = content,
            };
            if (Sensitive) request.MarkSensitive();
            return Task.FromResult(request);
        }

        private async Task<TestPage?> ReadFirstPage()
        {
            using HttpResponseMessage response = await HttpRuntime.SendAsync(CreateRequest, Publisher, Pipeline.Object);
            return await Deserialize(await response.Content.ReadAsStringAsync());
        }

        private Task<TestPage?> Deserialize(string body)
        {
            TestPage? page;
            if (Xml)
            {
                XElement element = XElement.Parse(body);
                page = new TestPage([(string)element.Attribute("name")!], (string?)element.Attribute("next"));
            }
            else
            {
                page = JsonSerializer.Deserialize<TestPage>(body);
            }
            if (page?.Items is { Length: > 0 } items) Deserialized.Add(items[0]);
            return Task.FromResult(page);
        }
    }

    private sealed class TrackedContent(string value) : StringContent(value)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
