// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewVirtualNetworkProcessor : NetworkProcessorBase<VirtualNetwork, VirtualNetwork>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
    public string ExtendedLocation { get; set; } = string.Empty;

    public List<string> AddressPrefixes { get; set; } = [];
    public List<string> IpamPoolPrefixAllocations { get; set; } = [];
    public List<Subnet> Subnets { get; set; } = [];
    public List<string> DnsServers { get; set; } = [];
    public List<string> IpAllocations { get; set; } = [];
    public List<string> SummarizedGatewayPrefixes { get; set; } = [];

    public bool EnableDdosProtection { get; set; }
    public string DdosProtectionPlan { get; set; } = string.Empty;
    public bool EnableVmProtection { get; set; }

    // 0 means "not specified"; the CLI binder has no nullable-int support.
    public int FlowTimeoutInMinutes { get; set; }

    public string BgpCommunity { get; set; } = string.Empty;
    public bool EnableEncryption { get; set; }
    public string EncryptionEnforcementPolicy { get; set; } = string.Empty;
    public string PrivateEndpointVNetPolicies { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{Name}?api-version={ApiVersion}";

    protected override VirtualNetwork? CreateRequestBody()
    {
        var properties = new VirtualNetworkProperties
        {
            AddressSpace = new AddressSpace
            {
                AddressPrefixes = AddressPrefixes,
                IpamPoolPrefixAllocations = IpamPoolPrefixAllocations
                    .Select(id => new IpamPoolPrefixAllocation { Pool = new SubResource { Id = id } })
                    .ToList()
            },
            Subnets = Subnets,
            VirtualNetworkPeerings = [],
            EnableDdosProtection = EnableDdosProtection,
            IpAllocations = IpAllocations.Select(id => new SubResource { Id = id }).ToList()
        };

        if (DnsServers.Count > 0)
        {
            properties.DhcpOptions = new DhcpOptions { DnsServers = DnsServers };
        }
        if (FlowTimeoutInMinutes > 0)
        {
            properties.FlowTimeoutInMinutes = FlowTimeoutInMinutes;
        }
        if (EnableVmProtection)
        {
            properties.EnableVmProtection = true;
        }
        if (DdosProtectionPlan.Length > 0)
        {
            properties.DdosProtectionPlan = new SubResource { Id = DdosProtectionPlan };
        }
        if (BgpCommunity.Length > 0)
        {
            properties.BgpCommunities = new VirtualNetworkBgpCommunities { VirtualNetworkCommunity = BgpCommunity };
        }
        if (EnableEncryption || EncryptionEnforcementPolicy.Length > 0)
        {
            properties.Encryption = new VirtualNetworkEncryption
            {
                Enabled = EnableEncryption,
                Enforcement = EncryptionEnforcementPolicy.Length > 0 ? EncryptionEnforcementPolicy : null
            };
        }
        if (PrivateEndpointVNetPolicies.Length > 0)
        {
            properties.PrivateEndpointVNetPolicies = PrivateEndpointVNetPolicies;
        }
        if (SummarizedGatewayPrefixes.Count > 0)
        {
            properties.SummarizedGatewayPrefixes = new AddressSpace { AddressPrefixes = SummarizedGatewayPrefixes };
        }

        return new VirtualNetwork
        {
            Location = Location,
            Properties = properties,
            Tags = Tags,
            ExtendedLocation = ExtendedLocation.Length > 0
                ? new Models.ExtendedLocation { Name = ExtendedLocation }
                : null
        };
    }

    // swagger declares final-state-via azure-async-operation with a
    // final-state-schema, but ARM returns no such body here; fall back to no final URI.
    protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
