// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Network
{
    [ExtensionVerb("network")]
    public class ExtensionIndex : IExtensionIndex
    {
        private const string Assembly = "Microsoft.Azure.Azclips.Extensions.Network";
        private const string SubscriptionHelp = "Azure Subscription Id. Defaults to the active profile's subscription.";
        private const string NameHelp = "The name of the virtual network.";
        private const string ResourceGroupHelp = "The resource group name.";
        private const string AddressPrefixesHelp = "Address prefixes for the virtual network, e.g. 10.0.0.0/16. Defaults to 10.0.0.0/16 unless IPAM pool allocations are supplied.";
        private const string SubnetsHelp = "Subnets to create, one occurrence per subnet, each a space-separated list of KEY=VALUE pairs. Supported keys: name, address-prefix, address-prefixes, nsg, route-table, nat-gateway, default-outbound-access, service-endpoints, service-endpoint-policy, delegations, ipam-pool-prefix-allocations, disable-private-endpoint-network-policies, disable-private-link-service-network-policies. Multi-valued keys take a comma-separated value.";
        private const string PowerShellSubnetsHelp = "Subnet objects from New-AzVirtualNetworkSubnetConfig or hashtables describing subnets to create.";
        private const string SubnetNamesHelp = "Names of the subnets to create. Simple alternative to the compound subnet argument.";
        private const string SubnetAddressPrefixesHelp = "Address prefixes for each subnet, positionally matched to the subnet names.";
        private const string DnsServersHelp = "DNS server IP addresses for the virtual network.";
        private const string TagsHelp = "Resource tags in KEY=VALUE form (space-separated).";
        private const string DdosProtectionHelp = "Enable DDoS protection for the virtual network.";
        private const string DdosProtectionPlanHelp = "Resource id of the DDoS protection plan to associate.";
        private const string VmProtectionHelp = "Enable VM protection for every subnet in the virtual network.";
        private const string FlowTimeoutHelp = "Flow timeout in minutes for the virtual network, between 4 and 30.";
        private const string BgpCommunityHelp = "The BGP community associated with the virtual network.";
        private const string EnableEncryptionHelp = "Enable virtual network encryption.";
        private const string EncryptionPolicyHelp = "Behaviour when a VM without encryption joins an encrypted virtual network.";
        private const string ExtendedLocationHelp = "Name of the edge zone (extended location) to create the virtual network in.";
        private const string PeVnetPoliciesHelp = "Private endpoint VNet policies.";
        private const string IpAllocationsHelp = "Resource ids of the IP allocations to reference.";
        private const string IpamAllocationsHelp = "Resource ids of the IPAM pools to allocate address prefixes from.";
        private const string SummarizedGatewayPrefixesHelp = "Summarized gateway prefixes advertised for the virtual network.";
        private const string ExpandHelp = "Expand referenced resources, e.g. subnets/ipConfigurations.";
        private const string SubnetNameHelp = "The subnet name.";
        private const string VNetNameHelp = "The virtual network name.";
        private const string SubnetPrefixesHelp = "Address prefixes for the subnet in CIDR format.";
        private const string NetworkSecurityGroupHelp = "Name or resource id of the network security group to associate.";
        private const string RouteTableHelp = "Name or resource id of the route table to associate.";
        private const string NatGatewayHelp = "Name or resource id of the NAT gateway to associate.";
        private const string ServiceEndpointsHelp = "Services for which to enable service endpoints.";
        private const string ServiceEndpointPoliciesHelp = "Names or resource ids of service endpoint policies to associate.";
        private const string DelegatedServicesHelp = "Services to which to delegate the subnet, e.g. Microsoft.Web/serverFarms.";
        private const string SubnetIpamAllocationsHelp = "Resource ids of IPAM pools from which to allocate subnet prefixes.";
        private static readonly string[] s_encryptionPolicies = ["DropUnencrypted", "AllowUnencrypted"];
        private static readonly string[] s_peVnetPolicies = ["Disabled", "Basic"];
        private static readonly string[] s_privateEndpointPolicies = ["Disabled", "Enabled", "NetworkSecurityGroupEnabled", "RouteTableEnabled"];
        private static readonly string[] s_privateLinkServicePolicies = ["Disabled", "Enabled"];

        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_PS = new(
            Name: "NewVirtualNetworkProcessor_Customization",
            InvocationName: "New-AzVirtualNetwork",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewVirtualNetworkProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]              = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                        = new("Name", "Name", typeof(string), NameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                    = new("Location", "Location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]           = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["AddressPrefix"]               = new("AddressPrefixes", "AddressPrefix", typeof(List<string>), AddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Subnet"]                      = new("SubnetInputs", "Subnet", typeof(List<object>), PowerShellSubnetsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetNames"]                 = new("SubnetNames", "SubnetNames", typeof(List<string>), SubnetNamesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]       = new("SubnetAddressPrefixes", "SubnetAddressPrefixes", typeof(List<List<string>>), SubnetAddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsServer"]                   = new("DnsServers", "DnsServer", typeof(List<string>), DnsServersHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tag"]                         = new("TagSpecs", "Tag", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableDdosProtection"]        = new("EnableDdosProtection", "EnableDdosProtection", typeof(bool), DdosProtectionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DdosProtectionPlanId"]        = new("DdosProtectionPlan", "DdosProtectionPlanId", typeof(string), DdosProtectionPlanHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableVmProtection"]          = new("EnableVmProtection", "EnableVmProtection", typeof(bool), VmProtectionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["FlowTimeout"]                 = new("FlowTimeoutInMinutes", "FlowTimeout", typeof(int), FlowTimeoutHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["BgpCommunity"]                = new("BgpCommunity", "BgpCommunity", typeof(string), BgpCommunityHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableEncryption"]            = new("EnableEncryption", "EnableEncryption", typeof(bool), EnableEncryptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionEnforcementPolicy"] = new("EncryptionEnforcementPolicy", "EncryptionEnforcementPolicy", typeof(string), EncryptionPolicyHelp, IsRequired: false, Aliases: [], AllowedValues: s_encryptionPolicies),
                ["EdgeZone"]                    = new("ExtendedLocation", "EdgeZone", typeof(string), ExtendedLocationHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["PrivateEndpointVNetPoliciesValue"] = new("PrivateEndpointVNetPolicies", "PrivateEndpointVNetPoliciesValue", typeof(string), PeVnetPoliciesHelp, IsRequired: false, Aliases: [], AllowedValues: s_peVnetPolicies),
                ["IpAllocation"]                = new("IpAllocations", "IpAllocation", typeof(List<string>), IpAllocationsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpamPoolPrefixAllocation"]    = new("IpamPoolPrefixAllocations", "IpamPoolPrefixAllocation", typeof(List<string>), IpamAllocationsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["SummarizedGatewayPrefix"]     = new("SummarizedGatewayPrefixes", "SummarizedGatewayPrefix", typeof(List<string>), SummarizedGatewayPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["AddressPrefixes"] = "AddressPrefix",
                ["Subnets"]         = "Subnet",
                ["DnsServers"]      = "DnsServer",
                ["Tags"]            = "Tag",
            }
        );
        private static readonly CommandMetadata s_getVirtualNetwork_PS = new(
            Name: "GetVirtualNetworkProcessor",
            InvocationName: "Get-AzVirtualNetwork_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), ExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listVirtualNetwork_PS = new(
            Name: "ListVirtualNetworkProcessor",
            InvocationName: "Get-AzVirtualNetwork_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every virtual network in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteVirtualNetwork_PS = new(
            Name: "DeleteVirtualNetworkProcessor",
            InvocationName: "Remove-AzVirtualNetwork",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newSubnetConfig_PS = new(
            Name: "NewSubnetConfigProcessor_Customization",
            InvocationName: "New-AzVirtualNetworkSubnetConfig",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewSubnetConfigProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewSubnetConfigProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"]                              = new("Name", "Name", typeof(string), SubnetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["AddressPrefix"]                     = new("AddressPrefixes", "AddressPrefix", typeof(List<string>), SubnetPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupId"]            = new("NetworkSecurityGroupId", "NetworkSecurityGroupId", typeof(string), NetworkSecurityGroupHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["RouteTableId"]                      = new("RouteTableId", "RouteTableId", typeof(string), RouteTableHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["NatGatewayId"]                      = new("NatGatewayId", "NatGatewayId", typeof(string), NatGatewayHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ServiceEndpoint"]                   = new("ServiceEndpoints", "ServiceEndpoint", typeof(List<string>), ServiceEndpointsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ServiceEndpointPolicy"]             = new("ServiceEndpointPolicies", "ServiceEndpointPolicy", typeof(List<string>), ServiceEndpointPoliciesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DelegatedService"]                  = new("DelegatedServices", "DelegatedService", typeof(List<string>), DelegatedServicesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpamPoolPrefixAllocation"]          = new("IpamPoolPrefixAllocations", "IpamPoolPrefixAllocation", typeof(List<string>), SubnetIpamAllocationsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["PrivateEndpointNetworkPoliciesFlag"] = new("PrivateEndpointNetworkPolicies", "PrivateEndpointNetworkPoliciesFlag", typeof(string), "Network policies for private endpoints.", IsRequired: false, Aliases: [], AllowedValues: s_privateEndpointPolicies),
                ["PrivateLinkServiceNetworkPoliciesFlag"] = new("PrivateLinkServiceNetworkPolicies", "PrivateLinkServiceNetworkPoliciesFlag", typeof(string), "Network policies for private link services.", IsRequired: false, Aliases: [], AllowedValues: s_privateLinkServicePolicies),
                ["DefaultOutboundAccess"]             = new("DefaultOutboundAccess", "DefaultOutboundAccess", typeof(bool), "Whether default outbound access is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_CLI = new(
            Name: "NewVirtualNetworkProcessor_Customization",
            InvocationName: "network vnet create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewVirtualNetworkProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                    = new("Name", "name", typeof(string), NameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["location"]                = new("Location", "location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["address-prefixes"]        = new("AddressPrefixes", "address-prefixes", typeof(List<string>), AddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnets"]                 = new("SubnetSpecs", "subnets", typeof(List<List<string>>), SubnetsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-names"]            = new("SubnetNames", "subnet-names", typeof(List<string>), SubnetNamesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"] = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), SubnetAddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-servers"]             = new("DnsServers", "dns-servers", typeof(List<string>), DnsServersHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                    = new("TagSpecs", "tags", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ddos-protection"]         = new("EnableDdosProtection", "ddos-protection", typeof(bool), DdosProtectionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ddos-protection-plan"]    = new("DdosProtectionPlan", "ddos-protection-plan", typeof(string), DdosProtectionPlanHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["vm-protection"]           = new("EnableVmProtection", "vm-protection", typeof(bool), VmProtectionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["flowtimeout"]             = new("FlowTimeoutInMinutes", "flowtimeout", typeof(int), FlowTimeoutHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["bgp-community"]           = new("BgpCommunity", "bgp-community", typeof(string), BgpCommunityHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-encryption"]       = new("EnableEncryption", "enable-encryption", typeof(bool), EnableEncryptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-policy"]       = new("EncryptionEnforcementPolicy", "encryption-policy", typeof(string), EncryptionPolicyHelp, IsRequired: false, Aliases: [], AllowedValues: s_encryptionPolicies),
                ["extended-location"]       = new("ExtendedLocation", "extended-location", typeof(string), ExtendedLocationHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["pe-vnet-policies"]        = new("PrivateEndpointVNetPolicies", "pe-vnet-policies", typeof(string), PeVnetPoliciesHelp, IsRequired: false, Aliases: [], AllowedValues: s_peVnetPolicies),
                ["ip-allocations"]          = new("IpAllocations", "ip-allocations", typeof(List<string>), IpAllocationsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ipam-allocations"]        = new("IpamPoolPrefixAllocations", "ipam-allocations", typeof(List<string>), IpamAllocationsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["sgp"]                     = new("SummarizedGatewayPrefixes", "sgp", typeof(List<string>), SummarizedGatewayPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"]                              = "resource-group",
                ["n"]                              = "name",
                ["l"]                              = "location",
                ["encryption-enforcement-policy"]  = "encryption-policy",
                ["private-endpoint-vnet-policies"] = "pe-vnet-policies",
                ["ipam-pool-prefix-allocations"]   = "ipam-allocations",
                ["summarized-gateway-prefixes"]    = "sgp",
            }
        );
        private static readonly CommandMetadata s_getVirtualNetwork_CLI = new(
            Name: "GetVirtualNetworkProcessor",
            InvocationName: "network vnet show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), ExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_listVirtualNetwork_CLI = new(
            Name: "ListVirtualNetworkProcessor",
            InvocationName: "network vnet list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Omit to list every virtual network in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group" }
        );
        private static readonly CommandMetadata s_deleteVirtualNetwork_CLI = new(
            Name: "DeleteVirtualNetworkProcessor",
            InvocationName: "network vnet delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_newSubnet_CLI = new(
            Name: "NewSubnetProcessor_Customization",
            InvocationName: "network vnet subnet create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewSubnetProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewSubnetProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                       = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]                        = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]                             = new("VirtualNetworkName", "vnet-name", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                                  = new("Name", "name", typeof(string), SubnetNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["address-prefixes"]                      = new("AddressPrefixes", "address-prefixes", typeof(List<string>), SubnetPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group"]                = new("NetworkSecurityGroup", "network-security-group", typeof(string), NetworkSecurityGroupHelp, IsRequired: false, Aliases: ["-nsg"], AllowedValues: []),
                ["route-table"]                           = new("RouteTable", "route-table", typeof(string), RouteTableHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["nat-gateway"]                           = new("NatGateway", "nat-gateway", typeof(string), NatGatewayHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["service-endpoints"]                     = new("ServiceEndpoints", "service-endpoints", typeof(List<string>), ServiceEndpointsHelp, IsRequired: false, Aliases: ["-endpoints"], AllowedValues: []),
                ["service-endpoint-policy"]               = new("ServiceEndpointPolicies", "service-endpoint-policy", typeof(List<string>), ServiceEndpointPoliciesHelp, IsRequired: false, Aliases: ["-service-endpoint-policies", "-policies"], AllowedValues: []),
                ["delegations"]                           = new("DelegatedServices", "delegations", typeof(List<string>), DelegatedServicesHelp, IsRequired: false, Aliases: ["-delegated-services"], AllowedValues: []),
                ["ipam-allocations"]                      = new("IpamPoolPrefixAllocationSpecs", "ipam-allocations", typeof(List<List<string>>), SubnetIpamAllocationsHelp, IsRequired: false, Aliases: ["-ipam-pool-prefix-allocations"], AllowedValues: []),
                ["private-endpoint-network-policies"]     = new("PrivateEndpointNetworkPolicies", "private-endpoint-network-policies", typeof(string), "Network policies for private endpoints.", IsRequired: false, Aliases: ["-ple-network-policies"], AllowedValues: s_privateEndpointPolicies),
                ["private-link-service-network-policies"] = new("PrivateLinkServiceNetworkPolicies", "private-link-service-network-policies", typeof(string), "Network policies for private link services.", IsRequired: false, Aliases: ["-pls-network-policies"], AllowedValues: s_privateLinkServicePolicies),
                ["disable-private-endpoint-network-policies"] = new("DisablePrivateEndpointNetworkPolicies", "disable-private-endpoint-network-policies", typeof(bool), "Disable network policies for private endpoints.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["disable-private-link-service-network-policies"] = new("DisablePrivateLinkServiceNetworkPolicies", "disable-private-link-service-network-policies", typeof(bool), "Disable network policies for private link services.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sharing-scope"]                         = new("SharingScope", "sharing-scope", typeof(string), "Tenant sharing scope for the subnet.", IsRequired: false, Aliases: [], AllowedValues: ["DelegatedServices", "Tenant"]),
                ["default-outbound-access"]               = new("DefaultOutboundAccessValue", "default-outbound-access", typeof(bool), "Whether default outbound access is enabled.", IsRequired: false, Aliases: ["-default-outbound"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
                ["nsg"] = "network-security-group",
                ["endpoints"] = "service-endpoints",
                ["service-endpoint-policies"] = "service-endpoint-policy",
                ["policies"] = "service-endpoint-policy",
                ["delegated-services"] = "delegations",
                ["ipam-pool-prefix-allocations"] = "ipam-allocations",
                ["ple-network-policies"] = "private-endpoint-network-policies",
                ["pls-network-policies"] = "private-link-service-network-policies",
                ["default-outbound"] = "default-outbound-access",
            }
        );
        private static readonly CommandMetadata s_getSubnet_CLI = new(
            Name: "GetSubnetProcessor",
            InvocationName: "network vnet subnet show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetSubnetProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]       = new("VirtualNetworkName", "vnet-name", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), SubnetNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), ExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_listSubnet_CLI = new(
            Name: "ListSubnetProcessor",
            InvocationName: "network vnet subnet list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListSubnetProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]       = new("VirtualNetworkName", "vnet-name", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group" }
        );
        private static readonly CommandMetadata s_deleteSubnet_CLI = new(
            Name: "DeleteSubnetProcessor",
            InvocationName: "network vnet subnet delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteSubnetProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]       = new("VirtualNetworkName", "vnet-name", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), SubnetNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzVirtualNetwork"]      = s_newVirtualNetwork_PS,
            ["Get-AzVirtualNetwork_Get"]  = s_getVirtualNetwork_PS,
            ["Get-AzVirtualNetwork_List"] = s_listVirtualNetwork_PS,
            ["Remove-AzVirtualNetwork"]   = s_deleteVirtualNetwork_PS,
            ["New-AzVirtualNetworkSubnetConfig"] = s_newSubnetConfig_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["network vnet create"] = s_newVirtualNetwork_CLI,
            ["network vnet show"]   = s_getVirtualNetwork_CLI,
            ["network vnet list"]   = s_listVirtualNetwork_CLI,
            ["network vnet delete"] = s_deleteVirtualNetwork_CLI,
            ["network vnet subnet create"] = s_newSubnet_CLI,
            ["network vnet subnet show"]   = s_getSubnet_CLI,
            ["network vnet subnet list"]   = s_listSubnet_CLI,
            ["network vnet subnet delete"] = s_deleteSubnet_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Network processors accept the parameter; the Engine
        // resolves the named profile before the processor runs.
        static ExtensionIndex()
        {
            foreach (var metadata in s_cliTable.Values)
            {
                metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
            foreach (var metadata in s_psTable.Values)
            {
                metadata.Parameters.TryAdd("Profile", new ParameterMetadata("Profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
        }

        // ── Lookup ──
        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            var table = clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
            return table.TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Values,
                ClientType.CLI => s_cliTable.Values,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Keys,
                ClientType.CLI => s_cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
    }
}
