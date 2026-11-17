// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Core.Agent.Providers;

namespace Core.Agent.Tests;

/// <summary>
/// Tests for <see cref="EncryptedReasoningRequestHandler"/>. The builtin endpoint can send a
/// follow-up request to a region that cannot decrypt the replayed reasoning items, so the
/// handler removes those items before each request is sent.
/// </summary>
public class EncryptedReasoningRequestHandlerTests
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
    public async Task ReasoningItems_AreRemovedBeforeTheRequestIsSent()
    {
        var original = Json(HttpStatusCode.OK, "{}");
        var upstream = new Upstream(original);
        using var request = Request(RequestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");

        using var response = await EncryptedReasoningRequestHandler.SendWithoutReasoningAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Same(original, response);
        var sent = Assert.Single(upstream.Requests);
        Assert.Equal(request.Method, sent.Method);
        Assert.Equal(request.RequestUri, sent.RequestUri);
        Assert.Equal("Bearer token", sent.Headers.Authorization?.ToString());
        Assert.Equal("application/json", sent.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(upstream.Bodies[0].Length, upstream.ContentLengths[0]);

        var sentBody = JsonNode.Parse(upstream.Bodies[0])!;
        var input = sentBody["input"]!.AsArray();
        Assert.Equal(5, input.Count);
        Assert.DoesNotContain(input, IsReasoning);
        // Output ids could point at the dropped reasoning items; item_reference keeps its id.
        Assert.Equal(["ref_1"], input.Select(item => item!["id"]?.GetValue<string>()).OfType<string>());
        Assert.Equal(["call_1", "call_1"], input.Select(item => item!["call_id"]?.GetValue<string>()).OfType<string>());
        Assert.Equal("reasoning.encrypted_content", sentBody["include"]![0]!.GetValue<string>());
        Assert.Contains("列出资源组 · café <b>&", Encoding.UTF8.GetString(upstream.Bodies[0]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorResponse_IsReturnedUnchangedWithoutRetry()
    {
        var original = Json(HttpStatusCode.BadRequest, InvalidEncryptedContent);
        var upstream = new Upstream(original);
        using var request = Request(RequestBody);

        using var response = await EncryptedReasoningRequestHandler.SendWithoutReasoningAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Same(original, response);
        Assert.Single(upstream.Requests);
        // The handler does not read the response, so the runtime gets the whole body.
        Assert.Equal(InvalidEncryptedContent, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("""{"input":[{"role":"user","content":"hello"}]}""")]
    [InlineData("""{"input":[{"type":"message","id":"msg_1","role":"assistant","content":[{"type":"output_text","text":"done"}]}]}""")]
    [InlineData("""{"input":"hello"}""")]
    [InlineData("not json")]
    public async Task RequestsWithNoReasoningToRemove_AreSentUnchanged(string requestBody)
    {
        var upstream = new Upstream(Json(HttpStatusCode.OK, "{}"));
        using var request = Request(requestBody);

        using var response = await EncryptedReasoningRequestHandler.SendWithoutReasoningAsync(request, upstream.SendAsync, CancellationToken.None);

        // Ids stay when there is no reasoning item to drop.
        Assert.Same(request, Assert.Single(upstream.Requests));
        Assert.Equal(requestBody, Encoding.UTF8.GetString(upstream.Bodies[0]));
    }

    [Theory]
    [InlineData("text/plain", null)]
    [InlineData("application/json", "gzip")]
    public async Task RequestsThatAreNotPlainJson_AreSentUnchanged(string mediaType, string? contentEncoding)
    {
        var upstream = new Upstream(Json(HttpStatusCode.OK, "{}"));
        using var request = Request(RequestBody, mediaType);
        if (contentEncoding is not null)
            request.Content!.Headers.ContentEncoding.Add(contentEncoding);

        using var response = await EncryptedReasoningRequestHandler.SendWithoutReasoningAsync(request, upstream.SendAsync, CancellationToken.None);

        Assert.Same(request, Assert.Single(upstream.Requests));
    }

    [Fact]
    public async Task ServedModelHeader_IsStoredAndResponseIsUnchanged()
    {
        var original = Json(HttpStatusCode.OK, "{}");
        original.Headers.TryAddWithoutValidation(ServedModel.HeaderName, "gpt-5.4-mini-2026-03-17");
        var upstream = new Upstream(original);
        var servedModel = new ServedModel();
        using var request = Request(RequestBody);

        using var response = await EncryptedReasoningRequestHandler.SendAndRecordAsync(request, upstream.SendAsync, servedModel, CancellationToken.None);

        Assert.Same(original, response);
        Assert.Equal("gpt-5.4-mini-2026-03-17", servedModel.Latest);
        Assert.Equal("gpt-5.4-mini-2026-03-17", servedModel.Turn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("model with spaces")]
    public async Task MissingOrInvalidServedModelHeader_ClearsTheResponseModel(string? header)
    {
        var valid = Json(HttpStatusCode.OK, "{}");
        valid.Headers.TryAddWithoutValidation(ServedModel.HeaderName, "gpt-5.4-mini-2026-03-17");
        var next = Json(HttpStatusCode.OK, "{}");
        if (header is not null)
            next.Headers.TryAddWithoutValidation(ServedModel.HeaderName, header);
        var upstream = new Upstream(valid, next);
        var servedModel = new ServedModel();

        using var first = Request(RequestBody);
        using var firstResponse = await EncryptedReasoningRequestHandler.SendAndRecordAsync(first, upstream.SendAsync, servedModel, CancellationToken.None);
        Assert.Equal("gpt-5.4-mini-2026-03-17", servedModel.Response);
        using var second = Request(RequestBody);
        using var secondResponse = await EncryptedReasoningRequestHandler.SendAndRecordAsync(second, upstream.SendAsync, servedModel, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Null(servedModel.Response);
        Assert.Equal("gpt-5.4-mini-2026-03-17", servedModel.Latest);
    }

    private static bool IsReasoning(JsonNode? item) =>
        item?["type"]?.GetValue<string>() == "reasoning";

    private static HttpRequestMessage Request(string body, string mediaType = "application/json") =>
        new(HttpMethod.Post, new Uri("https://example.test/azclips/openai/responses"))
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

    // Like a network response, the body can be read only once unless it is buffered.
    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var content = new StreamContent(new ReadOnceStream(payload));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentLength = payload.Length;
        return new HttpResponseMessage(status) { Content = content };
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
