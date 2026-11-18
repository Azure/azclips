// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Sends a child GET processor and reads its body, for customizations that read a resource before
/// they write one.
/// </summary>
internal static class ChildResourceReader
{
    /// <summary>
    /// Sends <paramref name="child"/>, which the caller creates with
    /// <c>CreateExtendedProcessor&lt;T&gt;</c>, and returns its body as <typeparamref name="TResource"/>.
    /// A failed request throws its service error, so the caller sends no write request.
    /// </summary>
    internal static async Task<TResource> ReadAsync<TResource>(
        NetworkProcessorBase child,
        IEventPublisher publisher,
        JsonSerializerOptions jsonOptions)
        where TResource : class
    {
        using HttpResponseMessage response = await child.SendAsync();
        await response.EnsureSuccessAsync(publisher);

        return await response.Content.ReadFromJsonAsync<TResource>(jsonOptions)
            ?? throw new InvalidOperationException(
                $"The response to {response.RequestMessage?.RequestUri} has no {typeof(TResource).Name} body.");
    }
}
