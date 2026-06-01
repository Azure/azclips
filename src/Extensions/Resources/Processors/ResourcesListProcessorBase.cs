using System.Text.Json;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Wire shape of an ARM list response: a page that carries the items in
    /// <see cref="Value"/> and an optional <see cref="NextLink"/> to the next page.
    /// </summary>
    public interface IPagedResult<TItem>
    {
        TItem[] Value { get; }
        string? NextLink { get; }
    }

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

                using HttpResponseMessage firstResponse = await SendAsync();
                TPage? page = await DeserializePageOrNull(firstResponse);

                while (page is not null)
                {
                    items.AddRange(page.Value);

                    if (string.IsNullOrEmpty(page.NextLink))
                    {
                        break;
                    }

                    if (!Uri.TryCreate(page.NextLink, UriKind.Absolute, out Uri? nextUri))
                    {
                        throw new InvalidOperationException($"Service returned a nextLink that is not an absolute URI: {page.NextLink}");
                    }

                    using var nextRequest = new HttpRequestMessage(HttpMethod.Get, nextUri);
                    using HttpResponseMessage nextResponse = await HttpPipeline.SendAsync(nextRequest, Publisher);
                    await Publisher.Publish(Events.TracingHttpResponseMessage, nextResponse);
                    page = await DeserializePageOrNull(nextResponse);
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
                await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TPage).Name}. Raw body: {rawBody}", ex);
            }
        }
    }
}
