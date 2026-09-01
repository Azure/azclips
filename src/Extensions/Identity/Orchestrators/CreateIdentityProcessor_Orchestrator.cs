// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors;

public class CreateIdentityProcessor_Orchestrator : CreateIdentityProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ResolveSubscriptionId();
        if (string.IsNullOrWhiteSpace(Location))
        {
            Location = await ResolveResourceGroupLocationAsync();
        }

        return await base.SendAsync();
    }

    private async Task<string> ResolveResourceGroupLocationAsync()
    {
        if (string.IsNullOrWhiteSpace(ResourceGroup))
            throw new InvalidOperationException("--resource-group is required.");

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildResourceGroupUri(ResourceGroup));
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Core.Abstractions.Runtime.Events.TracingHttpResponseMessage, response);
        await response.EnsureSuccessAsync(Publisher);

        var resourceGroup = await response.Content.ReadFromJsonAsync<ResourceGroupLocation>(JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize resource group '{ResourceGroup}'.");

        if (string.IsNullOrWhiteSpace(resourceGroup.Location))
            throw new InvalidOperationException($"Resource group '{ResourceGroup}' did not return a location.");

        return resourceGroup.Location;
    }

    private sealed class ResourceGroupLocation
    {
        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }
}
