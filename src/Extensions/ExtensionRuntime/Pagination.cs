// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Net.Http;
using System.Threading.Tasks;
using Core.Abstractions.Runtime;
using Core.Abstractions;
using System.Text.Json;

public static class Pagination
{
    /// <summary>
    /// Follows every <c>nextLink</c> and returns the accumulated items without writing them.
    /// Use this when a caller needs to filter, sort or enrich the full result set before output;
    /// use <c>WriteOutputWithPaginationAsync</c> when the raw set should go straight out.
    /// </summary>
    public static async Task<List<TItem>> CollectWithNextLinkAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions)
    {
        HttpRequestMessage request = await getRequest();
        int page = 1;
        List<TItem> batchedItems = new List<TItem>();
        try
        {
            while ( body is not null && pageItems(body) is {} items)
            {
                batchedItems.AddRange(items);
                if (nextLink(body) is not {} next || string.IsNullOrWhiteSpace(next))
                {
                    break;
                }
                body = default;
                request = request.CloneAndDispose(new Uri(next), HttpMethod.Get);
                await publisher.Publish(Events.PaginationNextPage, new EventData { Message = $"Fetching page {++page} ..." });
                using HttpResponseMessage response = await httpPipeline.SendAsync(request, publisher);
                await publisher.Publish(Events.TracingHttpResponseMessage, response);
                try
                {
                    await response.EnsureSuccessAsync(publisher);
                }
                catch
                {
                    throw;
                }
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
            return batchedItems;
        }
        finally
        {
            request.Dispose();
        }
    }

    /// <summary>
    /// Follows every <c>nextLink</c> and writes the accumulated items as a single batch.
    /// </summary>
    public static async Task WriteOutputWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, OutputStreamDelegate writeOutput, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions)
    {
        List<TItem> batchedItems = await body.CollectWithNextLinkAsync(pageItems, nextLink, getRequest, publisher, httpPipeline, jsonOptions);
        await writeOutput(batchedItems);
    }

    /// <summary>
    /// Follows every continuation token and returns the accumulated items without writing them.
    /// Each page is the original request rebuilt with the token set, so request signing and the
    /// processor's own deserialization (e.g. XML) apply to every page, not only the first.
    /// </summary>
    /// <param name="continuationToken">Reads the next page's token from a page; empty ends paging.</param>
    /// <param name="setContinuationToken">Sets the token on the request parameter that sends it back.</param>
    /// <param name="getRequest">Builds the request from the processor's current parameters.</param>
    /// <param name="deserialize">The processor's response deserialization.</param>
    public static async Task<List<TItem>> CollectWithTokenAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> continuationToken, Action<string> setContinuationToken, Func<Task<HttpRequestMessage>> getRequest, Func<string, Task<TBody?>> deserialize, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline)
    {
        int page = 1;
        List<TItem> batchedItems = new List<TItem>();
        while (body is not null && pageItems(body) is {} items)
        {
            batchedItems.AddRange(items);
            if (continuationToken(body) is not {} token || string.IsNullOrWhiteSpace(token))
            {
                break;
            }
            setContinuationToken(token);
            body = default;
            await publisher.Publish(Events.PaginationNextPage, new EventData { Message = $"Fetching page {++page} ..." });
            using HttpResponseMessage response = await HttpRuntime.SendAsync(getRequest, publisher, httpPipeline);
            string rawBody;
            bool sensitive = response.RequestMessage.HasSensitiveDiagnostics();
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
                    body = await deserialize(rawBody);
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
        }
        return batchedItems;
    }

    /// <summary>
    /// Follows every continuation token and writes the accumulated items as a single batch.
    /// </summary>
    public static async Task WriteOutputWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> continuationToken, Action<string> setContinuationToken, Func<Task<HttpRequestMessage>> getRequest, Func<string, Task<TBody?>> deserialize, OutputStreamDelegate writeOutput, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline)
    {
        List<TItem> batchedItems = await body.CollectWithTokenAsync(pageItems, continuationToken, setContinuationToken, getRequest, deserialize, publisher, httpPipeline);
        await writeOutput(batchedItems);
    }
}