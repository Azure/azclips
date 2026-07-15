// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lists virtual machines in a resource group. ARM list endpoints return an envelope
/// <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps the envelope and
/// surfaces the flat array of virtual machines, transparently following <c>nextLink</c>
/// across all pages.
/// </summary>
public class ListVMProcessor : ComputeProcessorBase<VirtualMachineListResult>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            var items = new List<VirtualMachine>();
            var seenLinks = new HashSet<string>(StringComparer.Ordinal);

            using HttpResponseMessage firstResponse = await SendAsync();
            VirtualMachineListResult? page = await DeserializePageOrNull(firstResponse);

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

                await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
                using var nextRequest = new HttpRequestMessage(HttpMethod.Get, nextUri);
                await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
                HttpResponseMessage nextResponse = await HttpPipeline.SendAsync(nextRequest, Publisher);
                await Publisher.Publish(Events.TracingHttpResponseMessage, nextResponse);
                try
                {
                    await nextResponse.EnsureSuccessAsync(Publisher);
                }
                catch
                {
                    nextResponse.Dispose();
                    throw;
                }
                using (nextResponse)
                {
                    page = await DeserializePageOrNull(nextResponse);
                }

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
    private async Task<VirtualMachineListResult?> DeserializePageOrNull(HttpResponseMessage response)
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
            return JsonSerializer.Deserialize<VirtualMachineListResult>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
            throw new InvalidOperationException($"Failed to deserialize response body into {nameof(VirtualMachineListResult)}. Raw body: {rawBody}", ex);
        }
    }
}