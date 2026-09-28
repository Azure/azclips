// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class GetSubnetProcessor : NetworkProcessorBase<object, Subnet>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Expand { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VirtualNetworkName}/subnets/{Name}?api-version={ApiVersion}" +
        (Expand.Length > 0 ? $"&$expand={Uri.EscapeDataString(Expand)}" : string.Empty);
}
