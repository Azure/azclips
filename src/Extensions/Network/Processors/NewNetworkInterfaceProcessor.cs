// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewNetworkInterfaceProcessor : NetworkProcessorBase<NetworkInterface, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
    public string ExtendedLocation { get; set; } = string.Empty;

    public string IpConfigurationName { get; set; } = string.Empty;
    public string Subnet { get; set; } = string.Empty;
    public string PublicIpAddress { get; set; } = string.Empty;
    public string PrivateIpAddress { get; set; } = string.Empty;
    public string PrivateIpAllocationMethod { get; set; } = string.Empty;
    public string PrivateIpAddressVersion { get; set; } = string.Empty;

    public string NetworkSecurityGroup { get; set; } = string.Empty;
    public List<string> DnsServers { get; set; } = [];
    public string InternalDnsNameLabel { get; set; } = string.Empty;
    public bool EnableAcceleratedNetworking { get; set; }
    public bool EnableIpForwarding { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version={ApiVersion}";

    protected override NetworkInterface? CreateRequestBody() =>
        new()
        {
            Location = Location,
            Tags = Tags,
            ExtendedLocation = ExtendedLocation.Length > 0
                ? new Models.ExtendedLocation { Name = ExtendedLocation }
                : null,
            Properties = new NetworkInterfaceProperties
            {
                IpConfigurations =
                [
                    new NetworkInterfaceIpConfiguration
                    {
                        Name = IpConfigurationName,
                        Properties = new NetworkInterfaceIpConfigurationProperties
                        {
                            Subnet = Subnet.Length > 0 ? new ExpandableSubResource { Id = Subnet } : null,
                            PublicIpAddress = PublicIpAddress.Length > 0 ? new ExpandableSubResource { Id = PublicIpAddress } : null,
                            PrivateIpAddress = PrivateIpAddress.Length > 0 ? PrivateIpAddress : null,
                            PrivateIpAllocationMethod = PrivateIpAllocationMethod.Length > 0 ? PrivateIpAllocationMethod : null,
                            PrivateIpAddressVersion = PrivateIpAddressVersion.Length > 0 ? PrivateIpAddressVersion : null
                        }
                    }
                ],
                NetworkSecurityGroup = NetworkSecurityGroup.Length > 0 ? new ExpandableSubResource { Id = NetworkSecurityGroup } : null,
                DnsSettings = DnsServers.Count > 0 || InternalDnsNameLabel.Length > 0
                    ? new NetworkInterfaceDnsSettings
                    {
                        DnsServers = DnsServers.Count > 0 ? DnsServers : null,
                        InternalDnsNameLabel = InternalDnsNameLabel.Length > 0 ? InternalDnsNameLabel : null
                    }
                    : null,
                EnableAcceleratedNetworking = EnableAcceleratedNetworking ? true : null,
                EnableIpForwarding = EnableIpForwarding ? true : null
            }
        };

    // swagger declares final-state-via azure-async-operation with a final-state-schema, but ARM
    // returns no such body here; fall back to no final URI.
    protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
