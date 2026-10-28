// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class ListNetworkSecurityGroupProcessor_Customization : ListNetworkSecurityGroupProcessor
{
    protected override string CreateRequestUri() =>
        string.IsNullOrWhiteSpace(ResourceGroupName)
            ? $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/providers/Microsoft.Network/networkSecurityGroups?api-version={ApiVersion}"
            : base.CreateRequestUri();
}
