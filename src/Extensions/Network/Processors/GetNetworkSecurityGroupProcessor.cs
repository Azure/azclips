// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class GetNetworkSecurityGroupProcessor : NetworkProcessorBase<object, NetworkSecurityGroup>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Expand { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        var uri = $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{Name}?api-version={ApiVersion}";
        return Expand.Length > 0 ? $"{uri}&$expand={Uri.EscapeDataString(Expand)}" : uri;
    }
}
