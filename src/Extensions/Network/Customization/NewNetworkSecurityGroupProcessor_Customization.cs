// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network nsg create</c> / <c>New-AzNetworkSecurityGroup</c> surface onto the
/// generated <see cref="NewNetworkSecurityGroupProcessor"/>: <c>KEY[=VALUE]</c> tags, and an omitted
/// CLI <c>--location</c> defaulted from the resource group.
/// </summary>
public class NewNetworkSecurityGroupProcessor_Customization : NewNetworkSecurityGroupProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Location))
        {
            var processor = CreateExtendedProcessor<GetResourceGroupLocationProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
            });
            using var response = await processor.SendAsync();
            await response.EnsureSuccessAsync(Publisher);
            var resourceGroup = await response.Content.ReadFromJsonAsync<ResourceGroupLocation>(JsonOptions)
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
            Tags = [];
            foreach (var (key, value) in KeyValueTokens.Parse(TagSpecs, "--tags", allowBareKey: true))
            {
                Tags[key] = value;
            }
        }
        return base.CreateRequestBody();
    }
}
