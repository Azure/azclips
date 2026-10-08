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
    /// use <see cref="WriteOutputWithPaginationAsync"/> when the raw set should go straight out.
    /// </summary>
    public static async Task<List<TItem>> CollectWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions)
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
                if (response.Content is null) await publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                else if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync())) await publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                else
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<TBody>(rawBody, jsonOptions);
                    }
                    catch (JsonException)
                    {
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
        List<TItem> batchedItems = await body.CollectWithPaginationAsync(pageItems, nextLink, getRequest, publisher, httpPipeline, jsonOptions);
        await writeOutput(batchedItems);
    }
}
