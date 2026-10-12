// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Core.Agent.Providers;

namespace Core.Agent.Tests;

/// <summary>
/// Tests for <see cref="EncryptedReasoningRecoveryHandler"/>. The builtin endpoint can send a
/// follow-up request to a region that cannot decrypt the replayed reasoning items. That region
/// returns <c>400 invalid_encrypted_content</c>, and the handler retries once without them.
/// </summary>
public class EncryptedReasoningRecoveryHandlerTests
{
    private const string InvalidEncryptedContent =
        """{"error":{"message":"The encrypted content for item rs_1 could not be verified.","type":"invalid_request_error","param":null,"code":"invalid_encrypted_content"}}""";

    // A Responses API body as the runtime replays it: reasoning items with encrypted content,
    // output items with ids, and non-ASCII user text.
    private const string RequestBody = """
        {"model":"gpt-54-mini-infra","store":false,"include":["reasoning.encrypted_content"],"input":[
          {"role":"user","content":[{"type":"input_text","text":"列出资源组 · café <b>&"}]},
          {"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"gAAA1"},
          {"type":"function_call","id":"fc_1","call_id":"call_1","name":"run","arguments":"{}"},
          {"type":"function_call_output","call_id":"call_1","output":"ok"},
          {"type":"reasoning","id":"rs_2","summary":[],"encrypted_content":"gAAA2"},
          {"type":"message","id":"msg_1","role":"assistant","content":[{"type":"output_text","text":"done"}]},
          {"type":"item_reference","id":"ref_1"}
        ]}
        """;

    [Fact]
    public async Task InvalidEncryptedContent_RetriesOnceWithoutReasoning()
    {
        var events = new List<AgentEvent>();
        var handler = new EncryptedReasoningRecoveryHandler(events.Add);
        var upstream = new Upstream(
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent),
            Json(HttpStatusCode.OK, "{}"));
        using var request = Request(RequestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, upstream.Requests.Count);
        var retry = upstream.Requests[1];
        Assert.Equal(request.RequestUri, retry.RequestUri);
        Assert.Equal("Bearer token", retry.Headers.Authorization?.ToString());
        Assert.Equal("application/json", retry.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(upstream.Bodies[1].Length, upstream.ContentLengths[1]);

        var retryBody = JsonNode.Parse(upstream.Bodies[1])!;
        var input = retryBody["input"]!.AsArray();
        Assert.Equal(5, input.Count);
        Assert.DoesNotContain(input, IsReasoning);
        // Output ids could point at the dropped reasoning items; item_reference keeps its id.
        Assert.Equal(["ref_1"], input.Select(item => item!["id"]?.GetValue<string>()).OfType<string>());
        Assert.Equal(["call_1", "call_1"], input.Select(item => item!["call_id"]?.GetValue<string>()).OfType<string>());
        Assert.Equal("reasoning.encrypted_content", retryBody["include"]![0]!.GetValue<string>());
        Assert.Contains("列出资源组 · café <b>&", Encoding.UTF8.GetString(upstream.Bodies[1]), StringComparison.Ordinal);

        var recovered = Assert.IsType<LlmRequestRecovered>(Assert.Single(events));
        Assert.Equal("invalid_encrypted_content", recovered.Reason);
        Assert.Equal(400, recovered.StatusCode);
        Assert.Equal(2, recovered.RemovedReasoningItems);
        Assert.True(handler.StripsReasoning);
    }

    [Fact]
    public async Task AfterRecovery_LaterRequestsAreSentWithoutReasoning()
    {
        var events = new List<AgentEvent>();
        var handler = new EncryptedReasoningRecoveryHandler(events.Add);
        var upstream = new Upstream(
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent),
            Json(HttpStatusCode.OK, "{}"),
            Json(HttpStatusCode.OK, "{}"));
        using var first = Request(RequestBody);
        using var second = Request(RequestBody);

        using var firstResponse = await handler.SendWithRecoveryAsync(first, upstream.SendAsync, CancellationToken.None);
        using var secondResponse = await handler.SendWithRecoveryAsync(second, upstream.SendAsync, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(3, upstream.Requests.Count);
        Assert.DoesNotContain(JsonNode.Parse(upstream.Bodies[2])!["input"]!.AsArray(), IsReasoning);
        Assert.Single(events);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":"context_length_exceeded"}}""")]
    [InlineData(HttpStatusCode.BadRequest, "not json")]
    [InlineData(HttpStatusCode.InternalServerError, InvalidEncryptedContent)]
    public async Task OtherResponses_AreReturnedUnchanged(HttpStatusCode status, string body)
    {
        var events = new List<AgentEvent>();
        var handler = new EncryptedReasoningRecoveryHandler(events.Add);
        var original = Json(status, body);
        var upstream = new Upstream(original);
        using var request = Request(RequestBody);

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Same(original, response);
        Assert.Same(request, Assert.Single(upstream.Requests));
        // The handler inspected the body; the runtime must still be able to read all of it.
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
        Assert.Empty(events);
        Assert.False(handler.StripsReasoning);
    }

    [Theory]
    [InlineData("""{"input":[{"role":"user","content":"hello"}]}""")]
    [InlineData("""{"input":"hello"}""")]
    [InlineData("not json")]
    public async Task InvalidEncryptedContent_WithNoReasoningToRemove_IsReturnedUnchanged(string requestBody)
    {
        var handler = new EncryptedReasoningRecoveryHandler(null);
        var upstream = new Upstream(Json(HttpStatusCode.BadRequest, InvalidEncryptedContent));
        using var request = Request(requestBody);

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(upstream.Requests);
        Assert.False(handler.StripsReasoning);
    }

    [Fact]
    public async Task FailedRetry_IsReturnedWithoutAnotherRetry()
    {
        var handler = new EncryptedReasoningRecoveryHandler(null);
        var upstream = new Upstream(
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent),
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent));
        using var request = Request(RequestBody);

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, upstream.Requests.Count);
        Assert.Equal(InvalidEncryptedContent, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AfterRecovery_RejectedRequestIsNotRetriedAgain()
    {
        var handler = new EncryptedReasoningRecoveryHandler(null);
        var upstream = new Upstream(
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent),
            Json(HttpStatusCode.OK, "{}"),
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent));
        using var first = Request(RequestBody);
        using var second = Request(RequestBody);

        using var firstResponse = await handler.SendWithRecoveryAsync(first, upstream.SendAsync, CancellationToken.None);
        using var secondResponse = await handler.SendWithRecoveryAsync(second, upstream.SendAsync, CancellationToken.None);

        // The second request already went out without reasoning, so a retry could not change it.
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        Assert.Equal(3, upstream.Requests.Count);
        Assert.DoesNotContain(JsonNode.Parse(upstream.Bodies[2])!["input"]!.AsArray(), IsReasoning);
    }

    [Theory]
    [InlineData("text/plain", null)]
    [InlineData("application/json", "gzip")]
    public async Task RequestsThatAreNotPlainJson_AreSentUnchanged(string mediaType, string? contentEncoding)
    {
        var handler = new EncryptedReasoningRecoveryHandler(null);
        var upstream = new Upstream(Json(HttpStatusCode.BadRequest, InvalidEncryptedContent));
        using var request = Request(RequestBody, mediaType);
        if (contentEncoding is not null)
            request.Content!.Headers.ContentEncoding.Add(contentEncoding);

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Same(request, Assert.Single(upstream.Requests));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("deflate")]
    public async Task CompressedInvalidEncryptedContent_IsRecognized(string contentEncoding)
    {
        var handler = new EncryptedReasoningRecoveryHandler(null);
        var upstream = new Upstream(
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent, contentEncoding),
            Json(HttpStatusCode.OK, "{}"));
        using var request = Request(RequestBody);

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, upstream.Requests.Count);
    }

    [Fact]
    public async Task EventCallbackFailure_DoesNotFailTheRequest()
    {
        var handler = new EncryptedReasoningRecoveryHandler(_ => throw new InvalidOperationException("renderer failed"));
        var upstream = new Upstream(
            Json(HttpStatusCode.BadRequest, InvalidEncryptedContent),
            Json(HttpStatusCode.OK, "{}"));
        using var request = Request(RequestBody);

        using var response = await handler.SendWithRecoveryAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static bool IsReasoning(JsonNode? item) =>
        item?["type"]?.GetValue<string>() == "reasoning";

    private static HttpRequestMessage Request(string body, string mediaType = "application/json") =>
        new(HttpMethod.Post, new Uri("https://example.test/azclips/openai/responses"))
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

    // Like a network response, the body can be read only once unless it is buffered.
    private static HttpResponseMessage Json(HttpStatusCode status, string body, string? contentEncoding = null)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        if (contentEncoding is not null)
            payload = Compress(payload, contentEncoding);

        var content = new StreamContent(new ReadOnceStream(payload));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentLength = payload.Length;
        if (contentEncoding is not null)
            content.Headers.ContentEncoding.Add(contentEncoding);
        return new HttpResponseMessage(status) { Content = content };
    }

    private static byte[] Compress(byte[] data, string contentEncoding)
    {
        using var output = new MemoryStream();
        using (Stream compressor = contentEncoding switch
        {
            "gzip" => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
            "br" => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
            "deflate" => new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(contentEncoding)),
        })
        {
            compressor.Write(data);
        }
        return output.ToArray();
    }

    // Stands in for the upstream HTTP call: records each request and returns the queued
    // responses in order.
    private sealed class Upstream
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public Upstream(params HttpResponseMessage[] responses) => _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<byte[]> Bodies { get; } = [];
        public List<long?> ContentLengths { get; } = [];

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            // Reading ContentLength stores it in the request headers, as SocketsHttpHandler does.
            ContentLengths.Add(request.Content?.Headers.ContentLength);
            Bodies.Add(request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false));
            return _responses.Dequeue();
        }
    }

    private sealed class ReadOnceStream(byte[] buffer) : MemoryStream(buffer, writable: false)
    {
        public override bool CanSeek => false;
    }
}
