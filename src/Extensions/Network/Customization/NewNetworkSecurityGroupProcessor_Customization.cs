// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewNetworkSecurityGroupProcessor_Customization : NewNetworkSecurityGroupProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Location))
        {
            var processor = new GetResourceGroupLocationProcessor
            {
                SubscriptionId = SubscriptionId,
                ResourceGroupName = ResourceGroupName,
                HttpPipeline = HttpPipeline,
                Publisher = Publisher,
                JsonOptions = JsonOptions,
            };
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
        Tags = TagSpecs.Count == 0 ? null : ParseTags();
        return base.CreateRequestBody();
    }

    private Dictionary<string, string> ParseTags()
    {
        var tags = new Dictionary<string, string>();
        foreach (var token in TagSpecs)
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"'{token}' is not a valid --tags value; expected KEY=VALUE.");
            }
            var key = token[..separator].Trim();
            if (key.Length == 0)
            {
                throw new ArgumentException($"'{token}' is not a valid --tags value; expected a non-empty key.");
            }
            tags[key] = token[(separator + 1)..].Trim();
        }
        return tags;
    }
}
