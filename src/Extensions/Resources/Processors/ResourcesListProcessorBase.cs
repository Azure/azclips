using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Base for "list" processors. ARM list endpoints return an envelope
    /// <c>{ "value": [...], "nextLink": "..." }</c>; this base unwraps the envelope and
    /// surfaces the flat array of items, transparently following <c>nextLink</c> across all pages.
    /// Derived processors only implement <see cref="ResourcesProcessorBase.CreateRequestMessage"/>.
    /// </summary>
    /// <typeparam name="TItem">The element type surfaced to output.</typeparam>
    /// <typeparam name="TPage">The page envelope model implementing <see cref="IPagedResult{TItem}"/>.</typeparam>
    public abstract class ResourcesListProcessorBase<TItem, TPage> : ResourcesProcessorBase<TPage>
        where TPage : class, IPagedResult<TItem>
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                var items = new List<TItem>();
                var seenLinks = new HashSet<string>(StringComparer.Ordinal);

                using HttpResponseMessage firstResponse = await SendAsync();
                TPage? page = await DeserializePageOrNull(firstResponse);

                if (page is null)
                {
                    // No usable body on the first page: return without writing output, matching the
                    // default processor pipeline. (A legitimate empty list deserializes to a non-null
                    // page with an empty Value and is still written as an empty array.)
                    return;
                }

                while (page is not null)
                {
                    if (page.Value is not null)
                    {
                        items.AddRange(page.Value);
                    }

                    if (string.IsNullOrEmpty(page.NextLink))
                    {
                        break;
                    }

                    if (!Uri.TryCreate(page.NextLink, UriKind.Absolute, out Uri? nextUri))
                    {
                        throw new InvalidOperationException($"Service returned a nextLink that is not an absolute URI: {page.NextLink}");
                    }

                    if (!seenLinks.Add(page.NextLink))
                    {
                        throw new InvalidOperationException($"Service returned a cyclic nextLink; aborting pagination to avoid an infinite loop: {page.NextLink}");
                    }

                    await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
                    using var nextRequest = new HttpRequestMessage(HttpMethod.Get, nextUri);
                    await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
                    using HttpResponseMessage nextResponse = await SendRequestAsync(nextRequest);
                    page = await DeserializePageOrNull(nextResponse);

                    if (page is null)
                    {
                        // A nextLink page with no usable body would silently truncate the result
                        // set. Fail fast rather than returning a partial list that looks complete.
                        throw new InvalidOperationException($"Pagination returned no usable body for a nextLink page, which would truncate results: {nextUri}");
                    }
                }

                await WriteOutput(items.ToArray());
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>
        /// Deserializes a single page, applying the same null/empty/invalid-JSON guards as the
        /// default processor pipeline. Returns <c>null</c> when there is no usable body.
        /// </summary>
        private async Task<TPage?> DeserializePageOrNull(HttpResponseMessage response)
        {
            if (response.Content is null)
            {
                await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                return null;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<TPage>(rawBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                await Publisher.Publish(Events.ResponseInvalidJson, new EventData
                {
                    Message = $"response has invalid JSON for {response.RequestMessage?.RequestUri}",
                    ExtendedData = rawBody,
                });
                throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TPage).Name}. Raw body: {TruncateBodyForExceptionMessage(rawBody)}", ex);
            }
        }
    }
}
