// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network security group create. Adds the Name input and the location default on top of the
/// generated PUT. The request route and LRO polling stay in
/// <see cref="CreateOrUpdateNetworkSecurityGroupsProcessor"/>.
/// </summary>
public class CreateNetworkSecurityGroupProcessor_Customization : CreateOrUpdateNetworkSecurityGroupsProcessor
{
    public string Name
    {
        get => NetworkSecurityGroupName;
        set => NetworkSecurityGroupName = value;
    }

    /// <summary>CLI tags in KEY[=VALUE] form. They replace <c>Tags</c> when given.</summary>
    public string[]? TagsInput { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Tags = NetworkTagsParser.Parse(TagsInput) ?? Tags;

        if (string.IsNullOrWhiteSpace(Location))
        {
            Location = await ResourceGroupLocationResolver.GetLocationAsync(
                CreateExtendedProcessor<GetResourceGroupsProcessor>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                }),
                Publisher,
                JsonOptions);
        }

        return await base.SendAsync();
    }
}
