// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// CopilotRequestHandler / CopilotRequestContext are experimental in GitHub.Copilot.SDK 1.0.x.
#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using GitHub.Copilot;

namespace Core.Agent.Providers;

/// <summary>
/// Sends the builtin provider's model requests and recovers from
/// <c>400 invalid_encrypted_content</c>.
/// </summary>
/// <remarks>
/// <para>
/// The builtin endpoint returns reasoning as encrypted content (requests use
/// <c>store: false</c>), and the runtime replays those <c>reasoning</c> input
/// items on the next request. The endpoint's global load balancer can send that
/// next request to a model deployment in another region. That deployment cannot
/// decrypt the replayed items and rejects the whole request with
/// <c>400 invalid_encrypted_content</c>.
/// </para>
/// <para>
/// On that error this handler sends the request once more without the
/// <c>reasoning</c> items. It also removes the <c>id</c> of the other items
/// (<c>call_id</c> stays), so no item refers to a dropped reasoning item. After
/// the first recovery, later requests from this handler drop reasoning items
/// before they are sent, because the error would otherwise repeat on most turns.
/// Every other response, including a failed retry, goes back to the runtime
/// unchanged.
/// </para>
/// </remarks>
internal sealed class EncryptedReasoningRecoveryHandler : CopilotRequestHandler
{
    internal const string ErrorCode = "invalid_encrypted_content";

    // Largest 400 body (after decompression) that is inspected. The error body
    // is a few hundred bytes; a larger body is not this error.
    private const int MaxInspectBytes = 1024 * 1024;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        // HTTP API payload, not HTML: keep non-ASCII prompt text as-is.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The SDK's default client keeps HttpClient's 100 s timeout, which also covers
    // the wait for response headers of a long model turn. Use the agent's 10-minute
    // turn timeout instead; the runtime still cancels abandoned requests through
    // CopilotRequestContext.CancellationToken. Like the SDK default, the client does
    // not decompress: the runtime receives the raw bytes and Content-Encoding.
    private static readonly HttpClient SharedClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    {
        Timeout = TimeSpan.FromMinutes(10),
    };

    private readonly Action<AgentEvent>? _onEvent;
    private int _stripReasoning;

    /// <param name="onEvent">Receives <see cref="LlmRequestRecovered"/> after each recovery. May be null.</param>
    public EncryptedReasoningRecoveryHandler(Action<AgentEvent>? onEvent)
        : base(SharedClient)
    {
        _onEvent = onEvent;
    }

    /// <summary>True after the first recovery: later requests are sent without reasoning items.</summary>
    internal bool StripsReasoning => Volatile.Read(ref _stripReasoning) != 0;

    protected override Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CopilotRequestContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        return SendWithRecoveryAsync(request, (message, _) => SendDirectAsync(message, ctx), ctx.CancellationToken);
    }

    private Task<HttpResponseMessage> SendDirectAsync(HttpRequestMessage request, CopilotRequestContext ctx) =>
        base.SendRequestAsync(request, ctx);

    /// <summary>
    /// Sends <paramref name="request"/> through <paramref name="send"/> and applies the
    /// recovery. Kept apart from <see cref="CopilotRequestContext"/> so tests can drive it.
    /// </summary>
    internal async Task<HttpResponseMessage> SendWithRecoveryAsync(
        HttpRequestMessage request,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(send);

        var content = request.Content;
        if (content is null || content.Headers.ContentEncoding.Count != 0 || !IsJson(content.Headers.ContentType))
            return await send(request, ct).ConfigureAwait(false);

        var body = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var outgoing = request;
        if (StripsReasoning && TryStripReasoning(body, out var withoutReasoning, out _))
        {
            outgoing = CloneWithBody(request, withoutReasoning);
            body = withoutReasoning;
        }

        var response = await send(outgoing, ct).ConfigureAwait(false);
        bool rejected;
        try
        {
            rejected = await IsInvalidEncryptedContentAsync(response, ct).ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        if (!rejected || !TryStripReasoning(body, out var retryBody, out var removed))
            return response;

        // The retry request is not disposed here: the SDK may still be streaming the
        // response, and the request holds only managed buffers.
        var retry = CloneWithBody(request, retryBody);
        response.Dispose();
        Interlocked.Exchange(ref _stripReasoning, 1);
        Publish(new LlmRequestRecovered(ErrorCode, (int)HttpStatusCode.BadRequest, removed, DateTimeOffset.UtcNow));
        return await send(retry, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes <c>reasoning</c> items from the Responses API <c>input</c> array, and
    /// the <c>id</c> of every other item except <c>item_reference</c>. Returns false
    /// when the body is not a JSON object with an <c>input</c> array, or when the
    /// array has no reasoning items.
    /// </summary>
    internal static bool TryStripReasoning(byte[] body, [NotNullWhen(true)] out byte[]? stripped, out int removed)
    {
        stripped = null;
        removed = 0;
        try
        {
            if (JsonNode.Parse(body) is not JsonObject root || root["input"] is not JsonArray input)
                return false;

            var kept = new JsonArray();
            foreach (var item in input)
            {
                if (IsItemType(item, "reasoning"))
                {
                    removed++;
                    continue;
                }

                var copy = item?.DeepClone();
                // An output item id without its reasoning item can make the service
                // ask for the missing reasoning item, so drop the ids too.
                if (copy is JsonObject entry && !IsItemType(entry, "item_reference"))
                    entry.Remove("id");
                kept.Add(copy);
            }

            if (removed == 0)
                return false;

            root["input"] = kept;
            using var buffer = new MemoryStream(body.Length);
            using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
            {
                root.WriteTo(writer);
            }
            stripped = buffer.ToArray();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            // Not a body this handler understands: leave the request unchanged.
            removed = 0;
            return false;
        }
    }

    private static bool IsItemType(JsonNode? item, string type) =>
        item is JsonObject entry
        && entry["type"] is JsonValue value
        && value.TryGetValue<string>(out var actual)
        && string.Equals(actual, type, StringComparison.Ordinal);

    private static async Task<bool> IsInvalidEncryptedContentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = response.Content;
        if (response.StatusCode != HttpStatusCode.BadRequest
            || !IsJson(content.Headers.ContentType)
            || content.Headers.ContentLength is > MaxInspectBytes)
        {
            return false;
        }

        // Buffers the raw (possibly compressed) bytes. When this response is returned,
        // the SDK streams the same bytes and headers to the runtime, which would read
        // the whole error body anyway.
        var raw = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var json = Decode(raw, content.Headers.ContentEncoding);
        return json is not null && HasErrorCode(json);
    }

    // Returns the decoded body, or null for an unknown encoding, corrupt data, or a
    // decoded body larger than MaxInspectBytes.
    private static byte[]? Decode(byte[] raw, ICollection<string> encodings)
    {
        if (encodings.Count == 0)
            return raw;
        if (encodings.Count != 1)
            return null;

        var encoding = encodings.First().Trim();
        try
        {
            if (Is(encoding, "identity"))
                return raw;
            if (Is(encoding, "gzip") || Is(encoding, "x-gzip"))
                return ReadBounded(new GZipStream(new MemoryStream(raw), CompressionMode.Decompress));
            if (Is(encoding, "br"))
                return ReadBounded(new BrotliStream(new MemoryStream(raw), CompressionMode.Decompress));
            if (Is(encoding, "deflate"))
                return ReadBounded(new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress));
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            return null;
        }

        return null;

        static bool Is(string actual, string expected) =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[]? ReadBounded(Stream decoder)
    {
        using (decoder)
        {
            using var output = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = decoder.Read(chunk)) > 0)
            {
                if (output.Length + read > MaxInspectBytes)
                    return null;
                output.Write(chunk, 0, read);
            }
            return output.ToArray();
        }
    }

    private static bool HasErrorCode(byte[] json)
    {
        ReadOnlyMemory<byte> utf8 = json;
        if (utf8.Span.StartsWith(Utf8Bom))
            utf8 = utf8[Utf8Bom.Length..];
        try
        {
            using var document = JsonDocument.Parse(utf8);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                && code.ValueEquals(ErrorCode);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static bool IsJson(MediaTypeHeaderValue? contentType) =>
        contentType?.MediaType is { } mediaType
        && (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    private static HttpRequestMessage CloneWithBody(HttpRequestMessage source, byte[] body)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy,
            Content = new ByteArrayContent(body),
        };
        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (source.Content is not null)
        {
            foreach (var header in source.Content.Headers)
            {
                // ByteArrayContent computes its own length.
                if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        return clone;
    }

    private void Publish(AgentEvent ev)
    {
        try
        {
            _onEvent?.Invoke(ev);
        }
        catch (Exception)
        {
            // Display only: a renderer failure must not fail the model request.
        }
    }
}
