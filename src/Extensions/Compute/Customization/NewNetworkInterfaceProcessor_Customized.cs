// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal sealed class NewNetworkInterfaceProcessor_Customized : ComputeProcessorBase<object, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> SubnetIds { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public string NetworkSecurityGroupId { get; set; } = string.Empty;
    public string PublicIPAddressId { get; set; } = string.Empty;
    public bool? EnableAcceleratedNetworking { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version=2025-05-01";

    protected override object? CreateRequestBody()
    {
        Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase)
        {
            ["enableIPForwarding"] = false,
            ["ipConfigurations"] = SubnetIds.Select(CreateIPConfiguration).ToList(),
        };

        if (EnableAcceleratedNetworking is bool acceleratedNetworking)
        {
            properties["enableAcceleratedNetworking"] = acceleratedNetworking;
        }

        if (!string.IsNullOrWhiteSpace(NetworkSecurityGroupId))
        {
            properties["networkSecurityGroup"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = NetworkSecurityGroupId,
            };
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["location"] = Location,
            ["properties"] = properties,
        };
    }

    private Dictionary<string, object?> CreateIPConfiguration(string subnetId, int index)
    {
        List<string> subnetPrefixes = SubnetAddressPrefixes.ElementAtOrDefault(index) ?? [];
        Dictionary<string, object?> ipProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            ["subnet"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = subnetId,
                ["properties"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["addressPrefixes"] = subnetPrefixes,
                    ["serviceEndpoints"] = Array.Empty<object>(),
                    ["serviceEndpointPolicies"] = Array.Empty<object>(),
                    ["ipAllocations"] = Array.Empty<object>(),
                    ["delegations"] = Array.Empty<object>(),
                    ["applicationGatewayIPConfigurations"] = Array.Empty<object>(),
                    ["ipamPoolPrefixAllocations"] = Array.Empty<object>(),
                },
            },
            ["virtualNetworkTaps"] = Array.Empty<object>(),
            ["applicationGatewayBackendAddressPools"] = Array.Empty<object>(),
            ["loadBalancerBackendAddressPools"] = Array.Empty<object>(),
            ["loadBalancerInboundNatRules"] = Array.Empty<object>(),
            ["privateIPAllocationMethod"] = "Dynamic",
            ["primary"] = index == 0,
        };

        if (index == 0 && !string.IsNullOrWhiteSpace(PublicIPAddressId))
        {
            ipProperties["publicIPAddress"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = PublicIPAddressId,
            };
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = $"ipconfig{index}",
            ["properties"] = ipProperties,
        };
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}