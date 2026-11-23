// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network security group create. Parses CLI tags into <see cref="NewNetworkSecurityGroupProcessor.Tags"/>
/// and, when no location is given, takes the location of the resource group (ResourceGroups_Get through
/// <see cref="GetResourceGroupProcessor"/>) before the inherited PUT. The route, method, and api-version
/// stay in the atomic processors.
/// </summary>
public class NewNetworkSecurityGroupProcessor_Orchestrator : NewNetworkSecurityGroupProcessor
{
    /// <summary>CLI tags as <c>key[=value]</c> strings. When given, they replace <see cref="NewNetworkSecurityGroupProcessor.Tags"/>.</summary>
    public string[]? TagsInput { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Tags = NetworkTagsParser.ParseTags(TagsInput) ?? Tags;

        if (string.IsNullOrWhiteSpace(Location))
        {
            Location = await GetResourceGroupLocationAsync();
        }

        return await base.SendAsync();
    }

    private async Task<string> GetResourceGroupLocationAsync()
    {
        // A failed GET throws from the runtime, so the PUT is not sent.
        using HttpResponseMessage response = await CreateExtendedProcessor<GetResourceGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
        }).SendAsync();

        ResourceGroup? resourceGroup = await response.Content.ReadFromJsonAsync<ResourceGroup>(JsonOptions);
        if (string.IsNullOrWhiteSpace(resourceGroup?.Location))
        {
            throw AzclipsErrorException.UsageError(
                $"Could not get the location of resource group '{ResourceGroupName}'. Specify a location.");
        }

        return resourceGroup.Location;
    }
}
