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
        private const string NetworkSecurityGroupNameHelp = "The name of the network security group.";
        private const string NetworkSecurityGroupLocationHelp = "Location for the network security group.";
        private const string NetworkSecurityGroupExpandHelp = "Expand referenced resources, e.g. networkInterfaces or subnets.";
        private const string SubnetNameHelp = "The subnet name.";
        private const string SubnetExpandHelp = "Expand referenced resources, e.g. networkSecurityGroup or routeTable.";
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

        // ── Public IP help ──
        private const string PipNameHelp = "The name of the public IP address.";
        private const string PipLocationHelp = "Location for the public IP address.";
        private const string PipSkuHelp = "Name of the public IP address SKU. Defaults to Standard.";
        private const string PipTierHelp = "Tier of the public IP address SKU. Global is only supported for Standard SKU addresses.";
        private const string PipAllocationHelp = "IP address allocation method. Defaults to Dynamic for the Basic SKU and Static otherwise.";
        private const string PipVersionHelp = "IP address version. Defaults to IPv4.";
        private const string PipDnsNameHelp = "Globally unique DNS entry (the domain name label).";
        private const string PipDnsNameScopeHelp = "The domain name label scope, which selects the reuse policy hashed into the FQDN.";
        private const string PipReverseFqdnHelp = "Reverse FQDN (fully qualified domain name).";
        private const string PipIdleTimeoutHelp = "Idle timeout in minutes. Defaults to 4.";
        private const string PipIpAddressHelp = "The IP address to assign, for bring-your-own-IP addresses.";
        private const string PipIpTagsHelp = "IP tags in TYPE=VALUE form (space-separated), e.g. FirstPartyUsage=/Sql.";
        private const string PipPrefixHelp = "Name or resource id of a public IP prefix to allocate the address from.";
        private const string PipDdosModeHelp = "The DDoS protection mode of the public IP address.";
        private const string PipDdosPlanHelp = "Name or resource id of a DDoS protection plan. Can only be set when the protection mode is Enabled.";
        private const string PipDdosPolicyHelp = "Name or resource id of a DDoS custom policy to associate.";
        private const string PipZonesHelp = "Availability zones to provision the address into.";
        private const string PipEdgeZoneHelp = "Name of the edge zone (extended location) to create the public IP address in.";
        private const string PipExpandHelp = "Expand referenced resources, e.g. ipConfiguration.";
        private static readonly string[] s_pipSkus = ["Basic", "Standard", "StandardV2"];
        private static readonly string[] s_pipTiers = ["Regional", "Global"];
        private static readonly string[] s_pipAllocationMethods = ["Static", "Dynamic"];
        private static readonly string[] s_pipVersions = ["IPv4", "IPv6"];
        private static readonly string[] s_pipDnsNameScopes = ["TenantReuse", "SubscriptionReuse", "ResourceGroupReuse", "NoReuse"];
        private static readonly string[] s_pipDdosModes = ["VirtualNetworkInherited", "Enabled", "Disabled"];
        private static readonly string[] s_pipZones = ["1", "2", "3"];

        // ── Network interface help ──
        private const string NicNameHelp = "The name of the network interface.";
        private const string NicLocationHelp = "Location for the network interface.";
        private const string NicSubnetHelp = "Name or resource id of an existing subnet. A name requires --vnet-name.";
        private const string NicSubnetIdHelp = "Resource id of an existing subnet.";
        private const string NicVnetNameHelp = "Name of the virtual network that contains the subnet, when --subnet is a name.";
        private const string NicPublicIpHelp = "Name or resource id of an existing public IP address to associate.";
        private const string NicNsgHelp = "Name or resource id of an existing network security group to associate.";
        private const string NicPrivateIpHelp = "Static private IP address to use. A dynamic address is allocated when omitted.";
        private const string NicPrivateIpVersionHelp = "Version of the private IP address. Defaults to IPv4.";
        private const string NicIpConfigNameHelp = "Name of the IP configuration. Defaults to ipconfig1.";
        private const string NicAcceleratedNetworkingHelp = "Enable accelerated networking.";
        private const string NicIpForwardingHelp = "Enable IP forwarding.";
        private const string NicDnsServersHelp = "DNS server IP addresses for the network interface.";
        private const string NicInternalDnsNameHelp = "The internal DNS name label.";
        private const string NicEdgeZoneHelp = "Name of the edge zone (extended location) to create the network interface in.";
        private const string NicExpandHelp = "Expand referenced resources, e.g. ipConfigurations/publicIPAddress.";
        private const string NicTagsHelp = "Resource tags in KEY[=VALUE] form (space-separated); a bare KEY sets an empty value.";

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
        private static readonly CommandMetadata s_newNetworkSecurityGroup_PS = new(
            Name: "NewNetworkSecurityGroupProcessor_Customization",
            InvocationName: "New-AzNetworkSecurityGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkSecurityGroupProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NetworkSecurityGroupNameHelp, IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]          = new("Location", "Location", typeof(string), NetworkSecurityGroupLocationHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Tag"]               = new("TagSpecs", "Tag", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["ResourceName"] = "Name" }
        );
        private static readonly CommandMetadata s_getNetworkSecurityGroup_PS = new(
            Name: "GetNetworkSecurityGroupProcessor",
            InvocationName: "Get-AzNetworkSecurityGroup_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkSecurityGroupProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NetworkSecurityGroupNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), NetworkSecurityGroupExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listNetworkSecurityGroup_PS = new(
            Name: "ListNetworkSecurityGroupProcessor_Customization",
            InvocationName: "Get-AzNetworkSecurityGroup_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkSecurityGroupProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListNetworkSecurityGroupProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every network security group in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteNetworkSecurityGroup_PS = new(
            Name: "DeleteNetworkSecurityGroupProcessor",
            InvocationName: "Remove-AzNetworkSecurityGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkSecurityGroupProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NetworkSecurityGroupNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
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
        ) { IsLocal = true };
        private static readonly CommandMetadata s_getSubnetConfig_PS = new(
            Name: "GetSubnetProcessor",
            InvocationName: "Get-AzVirtualNetworkSubnetConfig_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetSubnetProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]     = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]  = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["VirtualNetworkName"] = new("VirtualNetworkName", "VirtualNetworkName", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]               = new("Name", "Name", typeof(string), SubnetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ExpandResource"]     = new("Expand", "ExpandResource", typeof(string), SubnetExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listSubnetConfig_PS = new(
            Name: "ListSubnetProcessor",
            InvocationName: "Get-AzVirtualNetworkSubnetConfig_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListSubnetProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]     = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]  = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["VirtualNetworkName"] = new("VirtualNetworkName", "VirtualNetworkName", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteSubnetConfig_PS = new(
            Name: "DeleteSubnetProcessor",
            InvocationName: "Remove-AzVirtualNetworkSubnetConfig",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteSubnetProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]     = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]  = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["VirtualNetworkName"] = new("VirtualNetworkName", "VirtualNetworkName", typeof(string), VNetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]               = new("Name", "Name", typeof(string), SubnetNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Public IP PS Command Metadata ──
        private static readonly CommandMetadata s_newPublicIpAddress_PS = new(
            Name: "NewPublicIpAddressProcessor_Customization",
            InvocationName: "New-AzPublicIpAddress",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewPublicIpAddressProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewPublicIpAddressProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]       = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                 = new("Name", "Name", typeof(string), PipNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]             = new("Location", "Location", typeof(string), PipLocationHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]    = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Sku"]                  = new("Sku", "Sku", typeof(string), PipSkuHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipSkus),
                ["Tier"]                 = new("SkuTier", "Tier", typeof(string), PipTierHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipTiers),
                ["AllocationMethod"]     = new("AllocationMethod", "AllocationMethod", typeof(string), PipAllocationHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipAllocationMethods),
                ["IpAddressVersion"]     = new("IpAddressVersion", "IpAddressVersion", typeof(string), PipVersionHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipVersions),
                ["DomainNameLabel"]      = new("DomainNameLabel", "DomainNameLabel", typeof(string), PipDnsNameHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DomainNameLabelScope"] = new("DomainNameLabelScope", "DomainNameLabelScope", typeof(string), PipDnsNameScopeHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipDnsNameScopes),
                ["ReverseFqdn"]          = new("ReverseFqdn", "ReverseFqdn", typeof(string), PipReverseFqdnHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["IdleTimeoutInMinutes"] = new("IdleTimeoutInMinutes", "IdleTimeoutInMinutes", typeof(int), PipIdleTimeoutHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpAddress"]            = new("IpAddress", "IpAddress", typeof(string), PipIpAddressHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpTag"]                = new("IpTagSpecs", "IpTag", typeof(List<string>), PipIpTagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicIpPrefix"]       = new("PublicIpPrefix", "PublicIpPrefix", typeof(string), PipPrefixHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DdosProtectionMode"]   = new("DdosProtectionMode", "DdosProtectionMode", typeof(string), PipDdosModeHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipDdosModes),
                ["DdosProtectionPlanId"] = new("DdosProtectionPlan", "DdosProtectionPlanId", typeof(string), PipDdosPlanHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DdosCustomPolicyId"]   = new("DdosCustomPolicy", "DdosCustomPolicyId", typeof(string), PipDdosPolicyHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Zone"]                 = new("Zones", "Zone", typeof(List<string>), PipZonesHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipZones),
                ["EdgeZone"]             = new("ExtendedLocation", "EdgeZone", typeof(string), PipEdgeZoneHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tag"]                  = new("TagSpecs", "Tag", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["Tags"]   = "Tag",
                ["IpTags"] = "IpTag",
                ["Zones"]  = "Zone",
            }
        );
        private static readonly CommandMetadata s_getPublicIpAddress_PS = new(
            Name: "GetPublicIpAddressProcessor",
            InvocationName: "Get-AzPublicIpAddress_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetPublicIpAddressProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetPublicIpAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), PipNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), PipExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listPublicIpAddress_PS = new(
            Name: "ListPublicIpAddressProcessor",
            InvocationName: "Get-AzPublicIpAddress_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListPublicIpAddressProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListPublicIpAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every public IP address in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deletePublicIpAddress_PS = new(
            Name: "DeletePublicIpAddressProcessor",
            InvocationName: "Remove-AzPublicIpAddress",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeletePublicIpAddressProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeletePublicIpAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), PipNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Network interface PS Command Metadata ──
        private static readonly CommandMetadata s_newNetworkInterface_PS = new(
            Name: "NewNetworkInterfaceProcessor_Customization",
            InvocationName: "New-AzNetworkInterface",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkInterfaceProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]              = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                        = new("Name", "Name", typeof(string), NicNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                    = new("Location", "Location", typeof(string), NicLocationHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]           = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubnetId"]                    = new("Subnet", "SubnetId", typeof(string), NicSubnetIdHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["PublicIpAddressId"]           = new("PublicIpAddress", "PublicIpAddressId", typeof(string), NicPublicIpHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupId"]      = new("NetworkSecurityGroup", "NetworkSecurityGroupId", typeof(string), NicNsgHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["PrivateIpAddress"]            = new("PrivateIpAddress", "PrivateIpAddress", typeof(string), NicPrivateIpHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpConfigurationName"]         = new("IpConfigurationName", "IpConfigurationName", typeof(string), NicIpConfigNameHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsServer"]                   = new("DnsServers", "DnsServer", typeof(List<string>), NicDnsServersHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["InternalDnsNameLabel"]        = new("InternalDnsNameLabel", "InternalDnsNameLabel", typeof(string), NicInternalDnsNameHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableAcceleratedNetworking"] = new("EnableAcceleratedNetworking", "EnableAcceleratedNetworking", typeof(bool), NicAcceleratedNetworkingHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableIPForwarding"]          = new("EnableIpForwarding", "EnableIPForwarding", typeof(bool), NicIpForwardingHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["EdgeZone"]                    = new("ExtendedLocation", "EdgeZone", typeof(string), NicEdgeZoneHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tag"]                         = new("TagSpecs", "Tag", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["DnsServers"] = "DnsServer",
                ["Tags"]       = "Tag",
            }
        );
        private static readonly CommandMetadata s_getNetworkInterface_PS = new(
            Name: "GetNetworkInterfaceProcessor",
            InvocationName: "Get-AzNetworkInterface_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkInterfaceProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NicNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), NicExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listNetworkInterface_PS = new(
            Name: "ListNetworkInterfaceProcessor",
            InvocationName: "Get-AzNetworkInterface_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkInterfaceProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every network interface in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteNetworkInterface_PS = new(
            Name: "DeleteNetworkInterfaceProcessor",
            InvocationName: "Remove-AzNetworkInterface",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkInterfaceProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), NicNameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
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
        private static readonly CommandMetadata s_newNetworkSecurityGroup_CLI = new(
            Name: "NewNetworkSecurityGroupProcessor_Customization",
            InvocationName: "network nsg create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkSecurityGroupProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NetworkSecurityGroupNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["location"]        = new("Location", "location", typeof(string), $"{NetworkSecurityGroupLocationHelp} Defaults to the resource group's location.", IsRequired: false, Aliases: ["l"], AllowedValues: []),
                ["tags"]            = new("TagSpecs", "tags", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["l"] = "location", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_getNetworkSecurityGroup_CLI = new(
            Name: "GetNetworkSecurityGroupProcessor",
            InvocationName: "network nsg show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkSecurityGroupProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NetworkSecurityGroupNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), NetworkSecurityGroupExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["expand"]          = new("Expand", "expand", typeof(string), SubnetExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_listNetworkSecurityGroup_CLI = new(
            Name: "ListNetworkSecurityGroupProcessor_Customization",
            InvocationName: "network nsg list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkSecurityGroupProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListNetworkSecurityGroupProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Omit to list every network security group in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group" }
        );
        private static readonly CommandMetadata s_deleteNetworkSecurityGroup_CLI = new(
            Name: "DeleteNetworkSecurityGroupProcessor",
            InvocationName: "network nsg delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkSecurityGroupProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NetworkSecurityGroupNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
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

        // ── Public IP CLI Command Metadata ──
        private static readonly CommandMetadata s_newPublicIpAddress_CLI = new(
            Name: "NewPublicIpAddressProcessor_Customization",
            InvocationName: "network public-ip create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewPublicIpAddressProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewPublicIpAddressProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]      = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                 = new("Name", "name", typeof(string), PipNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["location"]             = new("Location", "location", typeof(string), PipLocationHelp, IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["resource-group"]       = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["sku"]                  = new("Sku", "sku", typeof(string), PipSkuHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipSkus),
                ["tier"]                 = new("SkuTier", "tier", typeof(string), PipTierHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipTiers),
                ["allocation-method"]    = new("AllocationMethod", "allocation-method", typeof(string), PipAllocationHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipAllocationMethods),
                ["version"]              = new("IpAddressVersion", "version", typeof(string), PipVersionHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipVersions),
                ["dns-name"]             = new("DomainNameLabel", "dns-name", typeof(string), PipDnsNameHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-name-scope"]       = new("DomainNameLabelScope", "dns-name-scope", typeof(string), PipDnsNameScopeHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipDnsNameScopes),
                ["reverse-fqdn"]         = new("ReverseFqdn", "reverse-fqdn", typeof(string), PipReverseFqdnHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["idle-timeout"]         = new("IdleTimeoutInMinutes", "idle-timeout", typeof(int), PipIdleTimeoutHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ip-address"]           = new("IpAddress", "ip-address", typeof(string), PipIpAddressHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ip-tags"]              = new("IpTagSpecs", "ip-tags", typeof(List<string>), PipIpTagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-prefix"]     = new("PublicIpPrefix", "public-ip-prefix", typeof(string), PipPrefixHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["protection-mode"]      = new("DdosProtectionMode", "protection-mode", typeof(string), PipDdosModeHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipDdosModes),
                ["ddos-protection-plan"] = new("DdosProtectionPlan", "ddos-protection-plan", typeof(string), PipDdosPlanHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ddos-custom-policy"]   = new("DdosCustomPolicy", "ddos-custom-policy", typeof(string), PipDdosPolicyHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["zone"]                 = new("Zones", "zone", typeof(List<string>), PipZonesHelp, IsRequired: false, Aliases: ["z"], AllowedValues: s_pipZones),
                ["edge-zone"]            = new("ExtendedLocation", "edge-zone", typeof(string), PipEdgeZoneHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                 = new("TagSpecs", "tags", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"]                    = "resource-group",
                ["n"]                    = "name",
                ["l"]                    = "location",
                ["z"]                    = "zone",
                ["ddos-protection-mode"] = "protection-mode",
            }
        );
        private static readonly CommandMetadata s_getPublicIpAddress_CLI = new(
            Name: "GetPublicIpAddressProcessor",
            InvocationName: "network public-ip show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetPublicIpAddressProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetPublicIpAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), PipNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), PipExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_listPublicIpAddress_CLI = new(
            Name: "ListPublicIpAddressProcessor",
            InvocationName: "network public-ip list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListPublicIpAddressProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListPublicIpAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Omit to list every public IP address in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group" }
        );
        private static readonly CommandMetadata s_deletePublicIpAddress_CLI = new(
            Name: "DeletePublicIpAddressProcessor",
            InvocationName: "network public-ip delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeletePublicIpAddressProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeletePublicIpAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), PipNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );

        // ── Network interface CLI Command Metadata ──
        private static readonly CommandMetadata s_newNetworkInterface_CLI = new(
            Name: "NewNetworkInterfaceProcessor_Customization",
            InvocationName: "network nic create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkInterfaceProcessor_Customization",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]            = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                       = new("Name", "name", typeof(string), NicNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["location"]                   = new("Location", "location", typeof(string), NicLocationHelp, IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["resource-group"]             = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["subnet"]                     = new("Subnet", "subnet", typeof(string), NicSubnetHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["vnet-name"]                  = new("VnetName", "vnet-name", typeof(string), NicVnetNameHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address"]          = new("PublicIpAddress", "public-ip-address", typeof(string), NicPublicIpHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group"]     = new("NetworkSecurityGroup", "network-security-group", typeof(string), NicNsgHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["private-ip-address"]         = new("PrivateIpAddress", "private-ip-address", typeof(string), NicPrivateIpHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["private-ip-address-version"] = new("PrivateIpAddressVersion", "private-ip-address-version", typeof(string), NicPrivateIpVersionHelp, IsRequired: false, Aliases: [], AllowedValues: s_pipVersions),
                ["accelerated-networking"]     = new("EnableAcceleratedNetworking", "accelerated-networking", typeof(bool), NicAcceleratedNetworkingHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ip-forwarding"]              = new("EnableIpForwarding", "ip-forwarding", typeof(bool), NicIpForwardingHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-servers"]                = new("DnsServers", "dns-servers", typeof(List<string>), NicDnsServersHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["internal-dns-name"]          = new("InternalDnsNameLabel", "internal-dns-name", typeof(string), NicInternalDnsNameHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["edge-zone"]                  = new("ExtendedLocation", "edge-zone", typeof(string), NicEdgeZoneHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                       = new("TagSpecs", "tags", typeof(List<string>), NicTagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
                ["l"] = "location",
            }
        );
        private static readonly CommandMetadata s_getNetworkInterface_CLI = new(
            Name: "GetNetworkInterfaceProcessor",
            InvocationName: "network nic show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkInterfaceProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.GetNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NicNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), NicExpandHelp, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group", ["n"] = "name" }
        );
        private static readonly CommandMetadata s_listNetworkInterface_CLI = new(
            Name: "ListNetworkInterfaceProcessor",
            InvocationName: "network nic list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkInterfaceProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.ListNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Omit to list every network interface in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "resource-group" }
        );
        private static readonly CommandMetadata s_deleteNetworkInterface_CLI = new(
            Name: "DeleteNetworkInterfaceProcessor",
            InvocationName: "network nic delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkInterfaceProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.DeleteNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), NicNameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
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
            ["New-AzNetworkSecurityGroup"]      = s_newNetworkSecurityGroup_PS,
            ["Get-AzNetworkSecurityGroup_Get"]  = s_getNetworkSecurityGroup_PS,
            ["Get-AzNetworkSecurityGroup_List"] = s_listNetworkSecurityGroup_PS,
            ["Remove-AzNetworkSecurityGroup"]   = s_deleteNetworkSecurityGroup_PS,
            ["New-AzVirtualNetworkSubnetConfig"] = s_newSubnetConfig_PS,
            ["Get-AzVirtualNetworkSubnetConfig_Get"]  = s_getSubnetConfig_PS,
            ["Get-AzVirtualNetworkSubnetConfig_List"] = s_listSubnetConfig_PS,
            ["Remove-AzVirtualNetworkSubnetConfig"]   = s_deleteSubnetConfig_PS,
            ["New-AzPublicIpAddress"]      = s_newPublicIpAddress_PS,
            ["Get-AzPublicIpAddress_Get"]  = s_getPublicIpAddress_PS,
            ["Get-AzPublicIpAddress_List"] = s_listPublicIpAddress_PS,
            ["Remove-AzPublicIpAddress"]   = s_deletePublicIpAddress_PS,
            ["New-AzNetworkInterface"]      = s_newNetworkInterface_PS,
            ["Get-AzNetworkInterface_Get"]  = s_getNetworkInterface_PS,
            ["Get-AzNetworkInterface_List"] = s_listNetworkInterface_PS,
            ["Remove-AzNetworkInterface"]   = s_deleteNetworkInterface_PS,
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
            ["network nsg create"]  = s_newNetworkSecurityGroup_CLI,
            ["network nsg show"]    = s_getNetworkSecurityGroup_CLI,
            ["network nsg list"]    = s_listNetworkSecurityGroup_CLI,
            ["network nsg delete"]  = s_deleteNetworkSecurityGroup_CLI,
            ["network vnet subnet create"] = s_newSubnet_CLI,
            ["network vnet subnet show"]   = s_getSubnet_CLI,
            ["network vnet subnet list"]   = s_listSubnet_CLI,
            ["network vnet subnet delete"] = s_deleteSubnet_CLI,
            ["network public-ip create"] = s_newPublicIpAddress_CLI,
            ["network public-ip show"]   = s_getPublicIpAddress_CLI,
            ["network public-ip list"]   = s_listPublicIpAddress_CLI,
            ["network public-ip delete"] = s_deletePublicIpAddress_CLI,
            ["network nic create"] = s_newNetworkInterface_CLI,
            ["network nic show"]   = s_getNetworkInterface_CLI,
            ["network nic list"]   = s_listNetworkInterface_CLI,
            ["network nic delete"] = s_deleteNetworkInterface_CLI,
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
