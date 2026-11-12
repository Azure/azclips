// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;

public static class Pagination
{
    /// <summary>
    /// Reads typed pages lazily, following next-link URLs. The first request runs only when
    /// enumeration starts; subsequent requests run only when the consumer advances.
    /// Cancellation uses the execution publisher's token.
    /// </summary>
    public static async IAsyncEnumerable<TBody> ReadWithNextLinkAsync<TBody>(
        Func<Task<TBody?>> readFirstPage,
        Func<TBody, string?> nextLink,
        Func<Task<HttpRequestMessage>> getRequest,
        JsonSerializerOptions jsonOptions,
        IEventPublisher publisher,
        IRuntimeHttpPipeline httpPipeline)
    {
        publisher.Token.ThrowIfCancellationRequested();
        TBody? body = await readFirstPage();
        HttpRequestMessage request = await getRequest();
        int page = 1;
        try
        {
            while (body is not null)
            {
                publisher.Token.ThrowIfCancellationRequested();
                yield return body;
                publisher.Token.ThrowIfCancellationRequested();
                if (nextLink(body) is not { } next || string.IsNullOrWhiteSpace(next))
                {
                    yield break;
                }

                request = request.CloneAndDispose(new Uri(next), HttpMethod.Get);
                await publisher.Publish(Events.PaginationNextPage, new EventData { Message = $"Fetching page {++page} ..." });
                using HttpResponseMessage response = await httpPipeline.SendAsync(request, publisher);
                await publisher.Publish(Events.TracingHttpResponseMessage, response);
                await response.EnsureSuccessAsync(publisher);
                body = default;
                string rawBody;
                bool sensitive = request.HasSensitiveDiagnostics() || response.RequestMessage.HasSensitiveDiagnostics();
                if (response.Content is null)
                    await publisher.Publish(Events.ResponseNoContent, sensitive
                        ? "Response has no content. Sensitive details omitted."
                        : $"response has no content for {response.RequestMessage?.RequestUri}");
                else if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync()))
                    await publisher.Publish(Events.ResponseEmptyBody, sensitive
                        ? "Response has empty body. Sensitive details omitted."
                        : $"response has empty body for {response.RequestMessage?.RequestUri}");
                else
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<TBody>(rawBody, jsonOptions);
                    }
                    catch (JsonException)
                    {
                        if (sensitive)
                        {
                            await publisher.Publish(Events.ResponseInvalidJson, "Response has invalid JSON. Sensitive details omitted.");
                            throw new InvalidOperationException("Failed to deserialize the response. Sensitive details omitted.");
                        }
                        await publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TBody).Name}. Raw body: {rawBody}");
                    }
                }
            }
        }
        finally
        {
            request.Dispose();
        }
    }

    /// <summary>
    /// Reads typed pages lazily by setting the response token on the request parameter and
    /// rebuilding each request. Request signing and the supplied deserializer apply to every page.
    /// Cancellation uses the execution publisher's token.
    /// </summary>
    public static async IAsyncEnumerable<TBody> ReadWithTokenAsync<TBody>(
        Func<Task<TBody?>> readFirstPage,
        Func<TBody, string?> continuationToken,
        Action<string> setContinuationToken,
        Func<Task<HttpRequestMessage>> getRequest,
        Func<string, Task<TBody?>> deserialize,
        IEventPublisher publisher,
        IRuntimeHttpPipeline httpPipeline)
    {
        publisher.Token.ThrowIfCancellationRequested();
        TBody? body = await readFirstPage();
        int page = 1;
        while (body is not null)
        {
            publisher.Token.ThrowIfCancellationRequested();
            yield return body;
            publisher.Token.ThrowIfCancellationRequested();
            if (continuationToken(body) is not { } token || string.IsNullOrWhiteSpace(token))
            {
                yield break;
            }

            setContinuationToken(token);
            await publisher.Publish(Events.PaginationNextPage, new EventData { Message = $"Fetching page {++page} ..." });
            using HttpResponseMessage response = await HttpRuntime.SendAsync(getRequest, publisher, httpPipeline);
            body = await ReadTokenResponseAsync(response, deserialize, publisher);
        }
    }

    private static async Task<TBody?> ReadTokenResponseAsync<TBody>(HttpResponseMessage response, Func<string, Task<TBody?>> deserialize, IEventPublisher publisher)
    {
        bool sensitive = response.RequestMessage.HasSensitiveDiagnostics();
        if (response.Content is null)
        {
            await publisher.Publish(Events.ResponseNoContent, sensitive
                ? "Response has no content. Sensitive details omitted."
                : $"response has no content for {response.RequestMessage?.RequestUri}");
            return default;
        }

        string rawBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            await publisher.Publish(Events.ResponseEmptyBody, sensitive
                ? "Response has empty body. Sensitive details omitted."
                : $"response has empty body for {response.RequestMessage?.RequestUri}");
            return default;
        }

        try
        {
            return await deserialize(rawBody);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (sensitive)
            {
                await publisher.Publish(Events.ResponseInvalidJson, "Response could not be deserialized. Sensitive details omitted.");
                throw new InvalidOperationException("Failed to deserialize the response. Sensitive details omitted.");
            }
            await publisher.Publish(Events.ResponseInvalidJson, $"response could not be deserialized for {response.RequestMessage?.RequestUri}");
            throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TBody).Name}. Raw body: {rawBody}", ex);
        }
    }

    /// <summary>Collects page items without writing output. Stops when a page has no items property.</summary>
    public static async Task<List<TItem>> CollectItemsAsync<TBody, TItem>(
        this IAsyncEnumerable<TBody> pages, Func<TBody, IEnumerable<TItem>?> pageItems)
    {
        List<TItem> items = [];
        await foreach (TBody body in pages)
        {
            if (pageItems(body) is not { } page)
            {
                break;
            }
            items.AddRange(page);
        }
        return items;
    }

    /// <summary>Collects an already fetched first page and all its next-link pages without writing output.</summary>
    public static Task<List<TItem>> CollectWithNextLinkAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions) =>
        ReadWithNextLinkAsync(() => Task.FromResult(body), nextLink, getRequest, jsonOptions, publisher, httpPipeline)
            .CollectItemsAsync(pageItems);

    /// <summary>Collects all next-link pages and writes the accumulated items as a single batch.</summary>
    public static async Task WriteOutputWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, OutputStreamDelegate writeOutput, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions)
    {
        List<TItem> items = await body.CollectWithNextLinkAsync(pageItems, nextLink, getRequest, publisher, httpPipeline, jsonOptions);
        await writeOutput(items);
    }

    /// <summary>Collects an already fetched first page and all its token pages without writing output.</summary>
    public static Task<List<TItem>> CollectWithTokenAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> continuationToken, Action<string> setContinuationToken, Func<Task<HttpRequestMessage>> getRequest, Func<string, Task<TBody?>> deserialize, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline) =>
        ReadWithTokenAsync(() => Task.FromResult(body), continuationToken, setContinuationToken, getRequest, deserialize, publisher, httpPipeline)
            .CollectItemsAsync(pageItems);

    /// <summary>Collects all token pages and writes the accumulated items as a single batch.</summary>
    public static async Task WriteOutputWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> continuationToken, Action<string> setContinuationToken, Func<Task<HttpRequestMessage>> getRequest, Func<string, Task<TBody?>> deserialize, OutputStreamDelegate writeOutput, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline)
    {
        List<TItem> items = await body.CollectWithTokenAsync(pageItems, continuationToken, setContinuationToken, getRequest, deserialize, publisher, httpPipeline);
        await writeOutput(items);
    }
}
