// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// CopilotRequestHandler / CopilotRequestContext are experimental in GitHub.Copilot.SDK 1.0.x.
#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using GitHub.Copilot;

namespace Core.Agent.Providers;

/// <summary>
/// Sends the builtin provider's model requests without encrypted reasoning items.
/// </summary>
/// <remarks>
/// <para>
/// The builtin endpoint returns reasoning as encrypted content (requests use
/// <c>store: false</c>), and the runtime replays those <c>reasoning</c> input
/// items on the next request. The endpoint's global load balancer picks a region
/// for each request, so the next request can reach a model deployment in another
/// region. That deployment cannot decrypt the replayed items and rejects the
/// whole request with <c>400 invalid_encrypted_content</c>.
/// </para>
/// <para>
/// So this handler removes the <c>reasoning</c> items before each request is
/// sent. It also removes the <c>id</c> of the other items (<c>call_id</c>
/// stays), so no item refers to a dropped reasoning item. The model still gets
/// every message, tool call, and tool output, but not its earlier reasoning.
/// The handler does not retry: every response goes back to the runtime
/// unchanged.
/// </para>
/// </remarks>
internal sealed class EncryptedReasoningRequestHandler : CopilotRequestHandler
{
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

    public EncryptedReasoningRequestHandler()
        : base(SharedClient)
    {
    }

    protected override Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CopilotRequestContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        return SendWithoutReasoningAsync(request, (message, _) => SendDirectAsync(message, ctx), ctx.CancellationToken);
    }

    private Task<HttpResponseMessage> SendDirectAsync(HttpRequestMessage request, CopilotRequestContext ctx) =>
        base.SendRequestAsync(request, ctx);

    /// <summary>
    /// Sends <paramref name="request"/> through <paramref name="send"/> without its
    /// reasoning items. Kept apart from <see cref="CopilotRequestContext"/> so tests can drive it.
    /// </summary>
    internal static async Task<HttpResponseMessage> SendWithoutReasoningAsync(
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
        if (!TryStripReasoning(body, out var withoutReasoning))
            return await send(request, ct).ConfigureAwait(false);

        // The new request is not disposed here: the SDK may still be streaming the
        // response, and the request holds only managed buffers.
        return await send(CloneWithBody(request, withoutReasoning), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes <c>reasoning</c> items from the Responses API <c>input</c> array, and
    /// the <c>id</c> of every other item except <c>item_reference</c>. Returns false
    /// when the body is not a JSON object with an <c>input</c> array, or when the
    /// array has no reasoning items.
    /// </summary>
    internal static bool TryStripReasoning(byte[] body, [NotNullWhen(true)] out byte[]? stripped)
    {
        stripped = null;
        try
        {
            if (JsonNode.Parse(body) is not JsonObject root || root["input"] is not JsonArray input)
                return false;

            var removed = 0;
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
            return false;
        }
    }

    private static bool IsItemType(JsonNode? item, string type) =>
        item is JsonObject entry
        && entry["type"] is JsonValue value
        && value.TryGetValue<string>(out var actual)
        && string.Equals(actual, type, StringComparison.Ordinal);

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
}
