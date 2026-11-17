// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network nsg create</c> / <c>New-AzNetworkSecurityGroup</c> surface onto the
/// generated <see cref="CreateOrUpdateNetworkSecurityGroupsProcessor"/>: <c>KEY[=VALUE]</c> tags, and
/// an omitted CLI <c>--location</c> defaulted from the resource group.
/// </summary>
public class CreateOrUpdateNetworkSecurityGroupsProcessor_Customization : CreateOrUpdateNetworkSecurityGroupsProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Location))
        {
            var processor = CreateExtendedProcessor<GetResourceGroupsProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
            });
            using var response = await processor.SendAsync();
            await response.EnsureSuccessAsync(Publisher);
            var resourceGroup = await response.Content.ReadFromJsonAsync<ResourceGroup>(JsonOptions)
                ?? throw new InvalidOperationException($"Resource group '{ResourceGroupName}' returned an empty response.");
            Location = resourceGroup.Location;
        }

        return await base.SendAsync();
    }

    protected override NetworkSecurityGroup? CreateRequestBody()
    {
        if (TagSpecs.Count > 0)
        {
            // Last occurrence wins, as in Azure CLI's tag parser.
            var tags = new Dictionary<string, string>();
            foreach (var (key, value) in KeyValueTokens.Parse(TagSpecs, "--tags", allowBareKey: true))
            {
                tags[key] = value;
            }
            Tags = tags;
        }
        return base.CreateRequestBody();
    }
}
