// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lets the static customization helpers compose processors on behalf of an owner, the same way
/// an orchestrator does with <see cref="ProcessorBase.CreateExtendedProcessor{T}"/>.
/// </summary>
public abstract partial class ProcessorBase
{
    internal T CreateChild<T>(Action<T>? configure = null) where T : ProcessorBase, new() =>
        CreateExtendedProcessor(configure);

    internal JsonSerializerOptions Json => JsonOptions;

    /// <summary>Lets an orchestrator send a sibling processor's request as its own, keeping the route generated.</summary>
    internal Task<HttpRequestMessage> CreateRequest() => CreateRequestMessage();

    /// <summary>
    /// Sends this processor's GET and returns the body, or null when ARM answers 404. Any other
    /// failure still throws, as <see cref="SendAsync"/> would.
    /// </summary>
    internal async Task<T?> GetOrDefaultAsync<T>() where T : class
    {
        using HttpRequestMessage request = await CreateRequestMessage();
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await response.EnsureSuccessAsync(Publisher);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}
