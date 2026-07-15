// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Abstractions.Runtime;

/// <summary>Envelope shape for ARM list responses: <c>{ "value": [...], "nextLink": "..." }</c>.</summary>
public interface IArmListPage<TItem>
{
    TItem[]? Value { get; }
    string? NextLink { get; }
}

/// <summary>
/// Shared driver for ARM list endpoints. Unwraps the <c>{ value, nextLink }</c> envelope, follows
/// <c>nextLink</c> across all pages (with cyclic-link and non-absolute-URI guards), and writes the
/// flattened array. Centralizes what was previously copy-pasted into each <c>List*Processor</c>.
/// </summary>
public static class ArmListPaginator
{
    /// <summary>
    /// Sends the first page via <paramref name="sendFirstPage"/>, then walks <c>nextLink</c> pages
    /// using <paramref name="pipeline"/>, accumulating <typeparamref name="TItem"/> values and
    /// finally writing them as a <typeparamref name="TItem"/>[] through <paramref name="writeOutput"/>.
    /// </summary>
    public static async Task CollectAndWriteAsync<TPage, TItem>(
        IRuntimeHttpPipeline pipeline,
        IEventPublisher publisher,
        JsonSerializerOptions jsonOptions,
        OutputStreamDelegate writeOutput,
        Func<Task<HttpResponseMessage>> sendFirstPage)
        where TPage : class, IArmListPage<TItem>
    {
        await publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            var items = new List<TItem>();
            var seenLinks = new HashSet<string>(StringComparer.Ordinal);

            using HttpResponseMessage firstResponse = await sendFirstPage();
            TPage? page = await DeserializePageOrNull<TPage>(publisher, jsonOptions, firstResponse);

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

                if (string.IsNullOrWhiteSpace(page.NextLink))
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

                await publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
                using var nextRequest = new HttpRequestMessage(HttpMethod.Get, nextUri);
                await publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
                HttpResponseMessage nextResponse = await pipeline.SendAsync(nextRequest, publisher);
                await publisher.Publish(Events.TracingHttpResponseMessage, nextResponse);
                try
                {
                    await nextResponse.EnsureSuccessAsync(publisher);
                }
                catch
                {
                    nextResponse.Dispose();
                    throw;
                }
                using (nextResponse)
                {
                    page = await DeserializePageOrNull<TPage>(publisher, jsonOptions, nextResponse);
                }

                if (page is null)
                {
                    // A nextLink page with no usable body would silently truncate the result
                    // set. Fail fast rather than returning a partial list that looks complete.
                    throw new InvalidOperationException($"Pagination returned no usable body for a nextLink page, which would truncate results: {nextUri}");
                }
            }

            await writeOutput(items.ToArray());
        }
        finally
        {
            await publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    /// <summary>
    /// Deserializes a single page, applying the same null/empty/invalid-JSON guards as the
    /// default processor pipeline. Returns <c>null</c> when there is no usable body.
    /// </summary>
    private static async Task<TPage?> DeserializePageOrNull<TPage>(
        IEventPublisher publisher, JsonSerializerOptions jsonOptions, HttpResponseMessage response)
        where TPage : class
    {
        if (response.Content is null)
        {
            await publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
            return null;
        }

        string rawBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            await publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TPage>(rawBody, jsonOptions);
        }
        catch (JsonException ex)
        {
            await publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
            throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TPage).Name}. Raw body: {rawBody}", ex);
        }
    }
}
