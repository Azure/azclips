// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Network
{
    [ExtensionVerb("network")]
    public class ExtensionIndex : IExtensionIndex
    {
        // Only commands in the dispatch tables are exposed. Atomic New*/List*/ListAll* processors that
        // have a customization, and the external GetResourceGroupProcessor, are not registered.

        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_PS = new(
            Name: "NewVirtualNetworkProcessor_Orchestrator",
            InvocationName: "New-AzVirtualNetwork",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewVirtualNetworkProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]                   = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                             = new("VirtualNetworkName", "Name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["Location"]                         = new("Location", "Location", typeof(string), "The location of the resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]                = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AddressPrefix"]                    = new("AddressSpaceAddressPrefixes", "AddressPrefix", typeof(string[]), "Address prefixes of the virtual network. Default: 10.0.0.0/16, unless -IpamPoolPrefixAllocation is given.", IsRequired: false, Aliases: ["AddressPrefixes"], AllowedValues: []),
                ["SubnetNames"]                      = new("SubnetNames", "SubnetNames", typeof(List<string>), "Names of subnets to create with the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]            = new("SubnetAddressPrefixes", "SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes of each subnet; item i belongs to subnet name i.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsServer"]                        = new("DhcpOptionsDnsServers", "DnsServer", typeof(string[]), "DNS server IP addresses.", IsRequired: false, Aliases: ["DnsServers"], AllowedValues: []),
                ["Tag"]                              = new("Tags", "Tag", typeof(Dictionary<string, string>), "Key-value pairs in the form of a hash table.", IsRequired: false, Aliases: ["Tags"], AllowedValues: []),
                ["EnableDdosProtection"]             = new("EnableDdosProtection", "EnableDdosProtection", typeof(bool), "Enable DDoS protection for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DdosProtectionPlanId"]             = new("DdosProtectionPlanId", "DdosProtectionPlanId", typeof(string), "Resource ID of the DDoS protection plan.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableVmProtection"]               = new("EnableVmProtection", "EnableVmProtection", typeof(bool), "Enable VM protection for all subnets in the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["FlowTimeout"]                      = new("FlowTimeoutInMinutes", "FlowTimeout", typeof(int), "The FlowTimeout value (in minutes), from 4 to 30.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["BgpCommunity"]                     = new("BgpCommunitiesVirtualNetworkCommunity", "BgpCommunity", typeof(string), "The BGP community associated with the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableEncryption"]                 = new("EncryptionEnabled", "EnableEncryption", typeof(bool), "Enable encryption on the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionEnforcementPolicy"]      = new("EncryptionEnforcement", "EncryptionEnforcementPolicy", typeof(string), "Whether the encrypted virtual network allows VMs that do not support encryption.", IsRequired: false, Aliases: [], AllowedValues: ["DropUnencrypted", "AllowUnencrypted"]),
                ["EdgeZone"]                         = new("ExtendedLocationName", "EdgeZone", typeof(string), "The name of the edge zone.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PrivateEndpointVNetPoliciesValue"] = new("PrivateEndpointVNetPolicies", "PrivateEndpointVNetPoliciesValue", typeof(string), "Private endpoint virtual network policies.", IsRequired: false, Aliases: [], AllowedValues: ["Disabled", "Basic"]),
                ["IpAllocation"]                     = new("IpAllocationIds", "IpAllocation", typeof(string[]), "IP allocation resource IDs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpamPoolPrefixAllocation"]         = new("IpamPoolIds", "IpamPoolPrefixAllocation", typeof(string[]), "IPAM pool resource IDs for the address space.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SummarizedGatewayPrefix"]          = new("SummarizedGatewayPrefixesAddressPrefixes", "SummarizedGatewayPrefix", typeof(string[]), "Summarized gateway address prefixes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
                ["AddressPrefixes"] = "AddressPrefix",
                ["DnsServers"] = "DnsServer",
                ["Tags"] = "Tag",
            }
        );
        private static readonly CommandMetadata s_getVirtualNetwork_PS = new(
            Name: "GetVirtualNetworkProcessor",
            InvocationName: "Get-AzVirtualNetwork_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("VirtualNetworkName", "Name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_listVirtualNetwork_PS = new(
            Name: "ListVirtualNetworkProcessor_Orchestrator",
            InvocationName: "Get-AzVirtualNetwork_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListVirtualNetworkProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListVirtualNetworkProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every virtual network in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteVirtualNetwork_PS = new(
            Name: "DeleteVirtualNetworkProcessor",
            InvocationName: "Remove-AzVirtualNetwork",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("VirtualNetworkName", "Name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_newNetworkSecurityGroup_PS = new(
            Name: "NewNetworkSecurityGroupProcessor_Orchestrator",
            InvocationName: "New-AzNetworkSecurityGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkSecurityGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("NetworkSecurityGroupName", "Name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]          = new("Location", "Location", typeof(string), "The location of the resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Tag"]               = new("Tags", "Tag", typeof(Dictionary<string, string>), "Key-value pairs in the form of a hash table.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_getNetworkSecurityGroup_PS = new(
            Name: "GetNetworkSecurityGroupProcessor",
            InvocationName: "Get-AzNetworkSecurityGroup_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("NetworkSecurityGroupName", "Name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_listNetworkSecurityGroup_PS = new(
            Name: "ListNetworkSecurityGroupProcessor_Orchestrator",
            InvocationName: "Get-AzNetworkSecurityGroup_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkSecurityGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListNetworkSecurityGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every network security group in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteNetworkSecurityGroup_PS = new(
            Name: "DeleteNetworkSecurityGroupProcessor",
            InvocationName: "Remove-AzNetworkSecurityGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("NetworkSecurityGroupName", "Name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_newPublicIPAddress_PS = new(
            Name: "NewPublicIPAddressProcessor_Orchestrator",
            InvocationName: "New-AzPublicIpAddress",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewPublicIPAddressProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewPublicIPAddressProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]       = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                 = new("PublicIpAddressName", "Name", typeof(string), "The name of the public IP address.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["Location"]             = new("Location", "Location", typeof(string), "The location of the resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]    = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Sku"]                  = new("SkuName", "Sku", typeof(string), "The public IP SKU name. Default: Standard.", IsRequired: false, Aliases: [], AllowedValues: ["Basic", "Standard", "StandardV2"]),
                ["Tier"]                 = new("SkuTier", "Tier", typeof(string), "The public IP SKU tier.", IsRequired: false, Aliases: [], AllowedValues: ["Regional", "Global"]),
                ["AllocationMethod"]     = new("PublicIPAllocationMethod", "AllocationMethod", typeof(string), "The IP address allocation method. Default: Dynamic for the Basic SKU, else Static.", IsRequired: false, Aliases: [], AllowedValues: ["Static", "Dynamic"]),
                ["IpAddressVersion"]     = new("PublicIPAddressVersion", "IpAddressVersion", typeof(string), "The IP address version. Default: IPv4.", IsRequired: false, Aliases: [], AllowedValues: ["IPv4", "IPv6"]),
                ["DomainNameLabel"]      = new("DnsSettingsDomainNameLabel", "DomainNameLabel", typeof(string), "The relative DNS name label.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DomainNameLabelScope"] = new("DnsSettingsDomainNameLabelScope", "DomainNameLabelScope", typeof(string), "The scope of the DNS name label.", IsRequired: false, Aliases: [], AllowedValues: ["TenantReuse", "SubscriptionReuse", "ResourceGroupReuse", "NoReuse"]),
                ["ReverseFqdn"]          = new("DnsSettingsReverseFqdn", "ReverseFqdn", typeof(string), "The reverse FQDN.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IdleTimeoutInMinutes"] = new("IdleTimeoutInMinutes", "IdleTimeoutInMinutes", typeof(int), "The idle timeout in minutes, from 4 to 30. Default: 4.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpAddress"]            = new("IpAddress", "IpAddress", typeof(string), "The IP address to use.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpTag"]                = new("IpTagPairs", "IpTag", typeof(List<KeyValuePair<string, string>>), "IP tags as a hash table of tag type and tag value.", IsRequired: false, Aliases: ["IpTags"], AllowedValues: []),
                ["PublicIpPrefix"]       = new("PublicIpPrefixId", "PublicIpPrefix", typeof(string), "Name or ID of a public IP prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DdosProtectionMode"]   = new("DdosSettingsProtectionMode", "DdosProtectionMode", typeof(string), "The DDoS protection mode.", IsRequired: false, Aliases: [], AllowedValues: ["VirtualNetworkInherited", "Enabled", "Disabled"]),
                ["DdosProtectionPlanId"] = new("DdosProtectionPlanId", "DdosProtectionPlanId", typeof(string), "Name or ID of the DDoS protection plan.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DdosCustomPolicyId"]   = new("DdosCustomPolicyId", "DdosCustomPolicyId", typeof(string), "Name or ID of the DDoS custom policy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Zone"]                 = new("Zones", "Zone", typeof(string[]), "Availability zones.", IsRequired: false, Aliases: ["Zones"], AllowedValues: []),
                ["EdgeZone"]             = new("EdgeZone", "EdgeZone", typeof(string), "The name of the edge zone.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tag"]                  = new("Tags", "Tag", typeof(Dictionary<string, string>), "Key-value pairs in the form of a hash table.", IsRequired: false, Aliases: ["Tags"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
                ["IpTags"] = "IpTag",
                ["Zones"] = "Zone",
                ["Tags"] = "Tag",
            }
        );
        private static readonly CommandMetadata s_getPublicIPAddress_PS = new(
            Name: "GetPublicIPAddressProcessor",
            InvocationName: "Get-AzPublicIpAddress_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetPublicIPAddressProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetPublicIPAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("PublicIpAddressName", "Name", typeof(string), "The name of the public IP address.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_listPublicIPAddress_PS = new(
            Name: "ListPublicIPAddressProcessor_Orchestrator",
            InvocationName: "Get-AzPublicIpAddress_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListPublicIPAddressProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListPublicIPAddressProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every public IP address in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deletePublicIPAddress_PS = new(
            Name: "DeletePublicIPAddressProcessor",
            InvocationName: "Remove-AzPublicIpAddress",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeletePublicIPAddressProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeletePublicIPAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("PublicIpAddressName", "Name", typeof(string), "The name of the public IP address.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_newNetworkInterface_PS = new(
            Name: "NewNetworkInterfaceProcessor_Orchestrator",
            InvocationName: "New-AzNetworkInterface",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]              = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                        = new("NetworkInterfaceName", "Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["Location"]                    = new("Location", "Location", typeof(string), "The location of the resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]           = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubnetId"]                    = new("SubnetId", "SubnetId", typeof(string), "The resource ID of the subnet.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["PublicIpAddressId"]           = new("PublicIpAddressId", "PublicIpAddressId", typeof(string), "The resource ID of the public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupId"]      = new("NetworkSecurityGroupId", "NetworkSecurityGroupId", typeof(string), "The resource ID of the network security group.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PrivateIpAddress"]            = new("PrivateIpAddress", "PrivateIpAddress", typeof(string), "The static private IP address. Without it the allocation is dynamic.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IpConfigurationName"]         = new("IpConfigurationName", "IpConfigurationName", typeof(string), "The name of the IP configuration. Default: ipconfig1.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsServer"]                   = new("DnsSettingsDnsServers", "DnsServer", typeof(string[]), "DNS server IP addresses.", IsRequired: false, Aliases: ["DnsServers"], AllowedValues: []),
                ["InternalDnsNameLabel"]        = new("DnsSettingsInternalDnsNameLabel", "InternalDnsNameLabel", typeof(string), "The internal DNS name label.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableAcceleratedNetworking"] = new("EnableAcceleratedNetworking", "EnableAcceleratedNetworking", typeof(bool), "Enable accelerated networking.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableIPForwarding"]          = new("EnableIPForwarding", "EnableIPForwarding", typeof(bool), "Enable IP forwarding.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EdgeZone"]                    = new("EdgeZone", "EdgeZone", typeof(string), "The name of the edge zone.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tag"]                         = new("Tags", "Tag", typeof(Dictionary<string, string>), "Key-value pairs in the form of a hash table.", IsRequired: false, Aliases: ["Tags"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
                ["DnsServers"] = "DnsServer",
                ["Tags"] = "Tag",
            }
        );
        private static readonly CommandMetadata s_getNetworkInterface_PS = new(
            Name: "GetNetworkInterfaceProcessor",
            InvocationName: "Get-AzNetworkInterface_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("NetworkInterfaceName", "Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ExpandResource"]    = new("Expand", "ExpandResource", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_listNetworkInterface_PS = new(
            Name: "ListNetworkInterfaceProcessor_Orchestrator",
            InvocationName: "Get-AzNetworkInterface_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every network interface in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteNetworkInterface_PS = new(
            Name: "DeleteNetworkInterfaceProcessor",
            InvocationName: "Remove-AzNetworkInterface",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("NetworkInterfaceName", "Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: ["ResourceName"], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceName"] = "Name",
            }
        );

        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_CLI = new(
            Name: "NewVirtualNetworkProcessor_Orchestrator",
            InvocationName: "network vnet create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewVirtualNetworkProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                    = new("VirtualNetworkName", "name", typeof(string), "The virtual network (VNet) name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["location"]                = new("Location", "location", typeof(string), "Location. Values from: `az account list-locations`.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["address-prefixes"]        = new("AddressSpaceAddressPrefixes", "address-prefixes", typeof(List<string>), "Space-separated IP address prefixes in CIDR format. Default: 10.0.0.0/16, unless --ipam-allocations is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-names"]            = new("SubnetNames", "subnet-names", typeof(List<string>), "Space-separated names of subnets to create with the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"] = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), "Address prefixes of each subnet. Repeat once per subnet; item i belongs to subnet name i.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-servers"]             = new("DhcpOptionsDnsServers", "dns-servers", typeof(List<string>), "Space-separated DNS server IP addresses.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                    = new("TagsInput", "tags", typeof(string[]), "Space-separated tags: key[=value] [key[=value] ...].", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ddos-protection"]         = new("EnableDdosProtection", "ddos-protection", typeof(bool), "Control whether DDoS protection is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ddos-protection-plan"]    = new("DdosProtectionPlanId", "ddos-protection-plan", typeof(string), "Resource ID of the DDoS protection plan to associate with the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["vm-protection"]           = new("EnableVmProtection", "vm-protection", typeof(bool), "Enable VM protection for all subnets in the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["flowtimeout"]             = new("FlowTimeoutInMinutes", "flowtimeout", typeof(int), "The FlowTimeout value (in minutes) for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["bgp-community"]           = new("BgpCommunitiesVirtualNetworkCommunity", "bgp-community", typeof(string), "The BGP community associated with the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-encryption"]       = new("EncryptionEnabled", "enable-encryption", typeof(bool), "Enable encryption on the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-policy"]       = new("EncryptionEnforcement", "encryption-policy", typeof(string), "Whether the encrypted virtual network allows VMs that do not support encryption.", IsRequired: false, Aliases: ["encryption-enforcement-policy"], AllowedValues: ["DropUnencrypted", "AllowUnencrypted"]),
                ["extended-location"]       = new("ExtendedLocationName", "extended-location", typeof(string), "The name of the edge zone.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["pe-vnet-policies"]        = new("PrivateEndpointVNetPolicies", "pe-vnet-policies", typeof(string), "Private endpoint virtual network policies.", IsRequired: false, Aliases: ["private-endpoint-vnet-policies"], AllowedValues: ["Disabled", "Basic"]),
                ["ip-allocations"]          = new("IpAllocationIds", "ip-allocations", typeof(List<string>), "Space-separated IP allocation resource IDs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ipam-allocations"]        = new("IpamPoolIds", "ipam-allocations", typeof(List<string>), "Space-separated IPAM pool resource IDs for the address space.", IsRequired: false, Aliases: ["ipam-pool-prefix-allocations"], AllowedValues: []),
                ["sgp"]                     = new("SummarizedGatewayPrefixesAddressPrefixes", "sgp", typeof(List<string>), "Space-separated summarized gateway address prefixes in CIDR format.", IsRequired: false, Aliases: ["summarized-gateway-prefixes"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
                ["l"] = "location",
                ["encryption-enforcement-policy"] = "encryption-policy",
                ["private-endpoint-vnet-policies"] = "pe-vnet-policies",
                ["ipam-pool-prefix-allocations"] = "ipam-allocations",
                ["summarized-gateway-prefixes"] = "sgp",
            }
        );
        private static readonly CommandMetadata s_getVirtualNetwork_CLI = new(
            Name: "GetVirtualNetworkProcessor",
            InvocationName: "network vnet show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("VirtualNetworkName", "name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_listVirtualNetwork_CLI = new(
            Name: "ListVirtualNetworkProcessor_Orchestrator",
            InvocationName: "network vnet list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListVirtualNetworkProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListVirtualNetworkProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group. Omit to list every virtual network in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_deleteVirtualNetwork_CLI = new(
            Name: "DeleteVirtualNetworkProcessor",
            InvocationName: "network vnet delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("VirtualNetworkName", "name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_newSubnet_CLI = new(
            Name: "NewSubnetProcessor_Orchestrator",
            InvocationName: "network vnet subnet create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewSubnetProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewSubnetProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                               = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]                                = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]                                     = new("VirtualNetworkName", "vnet-name", typeof(string), "The virtual network (VNet) name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                                          = new("SubnetName", "name", typeof(string), "The subnet name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["address-prefixes"]                              = new("AddressPrefixes", "address-prefixes", typeof(List<string>), "Space-separated address prefixes in CIDR format.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group"]                        = new("NetworkSecurityGroupId", "network-security-group", typeof(string), "Name or ID of a network security group (NSG).", IsRequired: false, Aliases: ["nsg"], AllowedValues: []),
                ["route-table"]                                   = new("RouteTableId", "route-table", typeof(string), "Name or ID of a route table to associate with the subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["nat-gateway"]                                   = new("NatGatewayId", "nat-gateway", typeof(string), "Name or ID of a NAT gateway to attach.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["service-endpoints"]                             = new("ServiceEndpointNames", "service-endpoints", typeof(List<string>), "Space-separated list of services allowed private access to this subnet.", IsRequired: false, Aliases: ["endpoints"], AllowedValues: []),
                ["service-endpoint-policy"]                       = new("ServiceEndpointPolicyIds", "service-endpoint-policy", typeof(List<string>), "Space-separated list of names or IDs of service endpoint policies to apply.", IsRequired: false, Aliases: ["service-endpoint-policies", "policies"], AllowedValues: []),
                ["delegations"]                                   = new("DelegationValues", "delegations", typeof(List<string>), "Space-separated list of services to whom the subnet should be delegated (e.g. Microsoft.Sql/servers).", IsRequired: false, Aliases: ["delegated-services"], AllowedValues: []),
                ["ipam-allocations"]                              = new("IpamAllocations", "ipam-allocations", typeof(List<string>), "IPAM pool allocation: id=<pool-id> [number-of-ip-addresses=<n>], or a bare pool ID. Repeat for more allocations.", IsRequired: false, Aliases: ["ipam-pool-prefix-allocations"], AllowedValues: []),
                ["private-endpoint-network-policies"]             = new("PrivateEndpointNetworkPolicies", "private-endpoint-network-policies", typeof(string), "Manage network policies for private endpoint. Default: Disabled.", IsRequired: false, Aliases: ["ple-network-policies"], AllowedValues: ["Disabled", "Enabled", "NetworkSecurityGroupEnabled", "RouteTableEnabled"]),
                ["private-link-service-network-policies"]         = new("PrivateLinkServiceNetworkPolicies", "private-link-service-network-policies", typeof(string), "Manage network policies for private link service. Default: Enabled.", IsRequired: false, Aliases: ["pls-network-policies"], AllowedValues: ["Disabled", "Enabled"]),
                ["disable-private-endpoint-network-policies"]     = new("DisablePrivateEndpointNetworkPolicies", "disable-private-endpoint-network-policies", typeof(bool), "Deprecated. Use --private-endpoint-network-policies. True sends Disabled; false sends Enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["disable-private-link-service-network-policies"] = new("DisablePrivateLinkServiceNetworkPolicies", "disable-private-link-service-network-policies", typeof(bool), "Deprecated. Use --private-link-service-network-policies. True sends Disabled; false sends Enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sharing-scope"]                                 = new("SharingScope", "sharing-scope", typeof(string), "Set this property to Tenant to allow sharing the subnet with other subscriptions in your AAD tenant.", IsRequired: false, Aliases: [], AllowedValues: ["DelegatedServices", "Tenant"]),
                ["default-outbound-access"]                       = new("DefaultOutboundAccess", "default-outbound-access", typeof(bool), "Set this property to false to disable default outbound connectivity for all VMs in the subnet.", IsRequired: false, Aliases: ["default-outbound"], AllowedValues: []),
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
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]       = new("VirtualNetworkName", "vnet-name", typeof(string), "The virtual network (VNet) name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("SubnetName", "name", typeof(string), "The subnet name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_listSubnet_CLI = new(
            Name: "ListSubnetProcessor",
            InvocationName: "network vnet subnet list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListSubnetProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]       = new("VirtualNetworkName", "vnet-name", typeof(string), "The virtual network (VNet) name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_deleteSubnet_CLI = new(
            Name: "DeleteSubnetProcessor",
            InvocationName: "network vnet subnet delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteSubnetProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteSubnetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["vnet-name"]       = new("VirtualNetworkName", "vnet-name", typeof(string), "The virtual network (VNet) name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("SubnetName", "name", typeof(string), "The subnet name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_newNetworkSecurityGroup_CLI = new(
            Name: "NewNetworkSecurityGroupProcessor_Orchestrator",
            InvocationName: "network nsg create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkSecurityGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("NetworkSecurityGroupName", "name", typeof(string), "Name of the network security group.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["location"]        = new("Location", "location", typeof(string), "Location. Defaults to the location of the resource group.", IsRequired: false, Aliases: ["l"], AllowedValues: []),
                ["tags"]            = new("TagsInput", "tags", typeof(string[]), "Space-separated tags: key[=value] [key[=value] ...].", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
                ["l"] = "location",
            }
        );
        private static readonly CommandMetadata s_getNetworkSecurityGroup_CLI = new(
            Name: "GetNetworkSecurityGroupProcessor",
            InvocationName: "network nsg show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("NetworkSecurityGroupName", "name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_listNetworkSecurityGroup_CLI = new(
            Name: "ListNetworkSecurityGroupProcessor_Orchestrator",
            InvocationName: "network nsg list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkSecurityGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListNetworkSecurityGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group. Omit to list every network security group in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_deleteNetworkSecurityGroup_CLI = new(
            Name: "DeleteNetworkSecurityGroupProcessor",
            InvocationName: "network nsg delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("NetworkSecurityGroupName", "name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_newPublicIPAddress_CLI = new(
            Name: "NewPublicIPAddressProcessor_Orchestrator",
            InvocationName: "network public-ip create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewPublicIPAddressProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewPublicIPAddressProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]      = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                 = new("PublicIpAddressName", "name", typeof(string), "The name of the public IP address.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]       = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["location"]             = new("Location", "location", typeof(string), "Location. Values from: `az account list-locations`.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["sku"]                  = new("SkuName", "sku", typeof(string), "Name of the public IP SKU. Default: Standard.", IsRequired: false, Aliases: [], AllowedValues: ["Basic", "Standard", "StandardV2"]),
                ["tier"]                 = new("SkuTier", "tier", typeof(string), "Tier of the public IP SKU.", IsRequired: false, Aliases: [], AllowedValues: ["Regional", "Global"]),
                ["allocation-method"]    = new("PublicIPAllocationMethod", "allocation-method", typeof(string), "IP address allocation method. Default: Dynamic for the Basic SKU, else Static.", IsRequired: false, Aliases: [], AllowedValues: ["Static", "Dynamic"]),
                ["version"]              = new("PublicIPAddressVersion", "version", typeof(string), "IP address type. Default: IPv4.", IsRequired: false, Aliases: [], AllowedValues: ["IPv4", "IPv6"]),
                ["dns-name"]             = new("DnsSettingsDomainNameLabel", "dns-name", typeof(string), "Globally unique DNS entry.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-name-scope"]       = new("DnsSettingsDomainNameLabelScope", "dns-name-scope", typeof(string), "The scope of the DNS name label.", IsRequired: false, Aliases: [], AllowedValues: ["TenantReuse", "SubscriptionReuse", "ResourceGroupReuse", "NoReuse"]),
                ["reverse-fqdn"]         = new("DnsSettingsReverseFqdn", "reverse-fqdn", typeof(string), "Reverse FQDN (fully qualified domain name).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["idle-timeout"]         = new("IdleTimeoutInMinutes", "idle-timeout", typeof(int), "Idle timeout in minutes. Default: 4.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ip-address"]           = new("IpAddress", "ip-address", typeof(string), "The IP address associated with the public IP address resource.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ip-tags"]              = new("IpTagValues", "ip-tags", typeof(string[]), "Space-separated list of IP tags in TYPE=VALUE format.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-prefix"]     = new("PublicIpPrefixId", "public-ip-prefix", typeof(string), "Name or ID of a public IP prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["protection-mode"]      = new("DdosSettingsProtectionMode", "protection-mode", typeof(string), "The DDoS protection mode of the public IP.", IsRequired: false, Aliases: ["ddos-protection-mode"], AllowedValues: ["VirtualNetworkInherited", "Enabled", "Disabled"]),
                ["ddos-protection-plan"] = new("DdosProtectionPlanId", "ddos-protection-plan", typeof(string), "Name or ID of a DDoS protection plan associated with the public IP.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ddos-custom-policy"]   = new("DdosCustomPolicyId", "ddos-custom-policy", typeof(string), "Name or ID of a DDoS custom policy associated with the public IP.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["zone"]                 = new("Zones", "zone", typeof(List<string>), "Space-separated list of availability zones.", IsRequired: false, Aliases: ["z"], AllowedValues: ["1", "2", "3"]),
                ["edge-zone"]            = new("EdgeZone", "edge-zone", typeof(string), "The name of the edge zone.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                 = new("TagsInput", "tags", typeof(string[]), "Space-separated tags: key=value [key=value ...].", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
                ["l"] = "location",
                ["ddos-protection-mode"] = "protection-mode",
                ["z"] = "zone",
            }
        );
        private static readonly CommandMetadata s_getPublicIPAddress_CLI = new(
            Name: "GetPublicIPAddressProcessor",
            InvocationName: "network public-ip show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetPublicIPAddressProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetPublicIPAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("PublicIpAddressName", "name", typeof(string), "The name of the public IP address.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_listPublicIPAddress_CLI = new(
            Name: "ListPublicIPAddressProcessor_Orchestrator",
            InvocationName: "network public-ip list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListPublicIPAddressProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListPublicIPAddressProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group. Omit to list every public IP address in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_deletePublicIPAddress_CLI = new(
            Name: "DeletePublicIPAddressProcessor",
            InvocationName: "network public-ip delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeletePublicIPAddressProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeletePublicIPAddressProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("PublicIpAddressName", "name", typeof(string), "The name of the public IP address.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_newNetworkInterface_CLI = new(
            Name: "NewNetworkInterfaceProcessor_Orchestrator",
            InvocationName: "network nic create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]            = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                       = new("NetworkInterfaceName", "name", typeof(string), "The network interface (NIC) name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]             = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["location"]                   = new("Location", "location", typeof(string), "Location. Values from: `az account list-locations`.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["subnet"]                     = new("SubnetId", "subnet", typeof(string), "Name or ID of an existing subnet. If a name is given, also give --vnet-name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["vnet-name"]                  = new("VirtualNetworkName", "vnet-name", typeof(string), "The virtual network (VNet) name of the subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address"]          = new("PublicIpAddressId", "public-ip-address", typeof(string), "Name or ID of an existing public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group"]     = new("NetworkSecurityGroupId", "network-security-group", typeof(string), "Name or ID of an existing network security group.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["private-ip-address"]         = new("PrivateIpAddress", "private-ip-address", typeof(string), "Static private IP address to use. Without it the allocation is dynamic.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["private-ip-address-version"] = new("PrivateIpAddressVersion", "private-ip-address-version", typeof(string), "The private IP address version to use.", IsRequired: false, Aliases: [], AllowedValues: ["IPv4", "IPv6"]),
                ["accelerated-networking"]     = new("EnableAcceleratedNetworking", "accelerated-networking", typeof(bool), "Enable accelerated networking.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ip-forwarding"]              = new("EnableIPForwarding", "ip-forwarding", typeof(bool), "Enable IP forwarding.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-servers"]                = new("DnsSettingsDnsServers", "dns-servers", typeof(List<string>), "Space-separated list of DNS server IP addresses.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["internal-dns-name"]          = new("DnsSettingsInternalDnsNameLabel", "internal-dns-name", typeof(string), "The internal DNS name label.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["edge-zone"]                  = new("EdgeZone", "edge-zone", typeof(string), "The name of the edge zone.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                       = new("TagsInput", "tags", typeof(string[]), "Space-separated tags: key[=value] [key[=value] ...].", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
                ["l"] = "location",
            }
        );
        private static readonly CommandMetadata s_getNetworkInterface_CLI = new(
            Name: "GetNetworkInterfaceProcessor",
            InvocationName: "network nic show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.GetNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.GetNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("NetworkInterfaceName", "name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), "Expands referenced resources.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_listNetworkInterface_CLI = new(
            Name: "ListNetworkInterfaceProcessor_Orchestrator",
            InvocationName: "network nic list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.ListNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.ListNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group. Omit to list every network interface in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_deleteNetworkInterface_CLI = new(
            Name: "DeleteNetworkInterfaceProcessor",
            InvocationName: "network nic delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.DeleteNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Network",
            ProcessorType: typeof(Processors.DeleteNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("NetworkInterfaceName", "name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzVirtualNetwork"] = s_newVirtualNetwork_PS,
            ["Get-AzVirtualNetwork_Get"] = s_getVirtualNetwork_PS,
            ["Get-AzVirtualNetwork_List"] = s_listVirtualNetwork_PS,
            ["Remove-AzVirtualNetwork"] = s_deleteVirtualNetwork_PS,
            ["New-AzNetworkSecurityGroup"] = s_newNetworkSecurityGroup_PS,
            ["Get-AzNetworkSecurityGroup_Get"] = s_getNetworkSecurityGroup_PS,
            ["Get-AzNetworkSecurityGroup_List"] = s_listNetworkSecurityGroup_PS,
            ["Remove-AzNetworkSecurityGroup"] = s_deleteNetworkSecurityGroup_PS,
            ["New-AzPublicIpAddress"] = s_newPublicIPAddress_PS,
            ["Get-AzPublicIpAddress_Get"] = s_getPublicIPAddress_PS,
            ["Get-AzPublicIpAddress_List"] = s_listPublicIPAddress_PS,
            ["Remove-AzPublicIpAddress"] = s_deletePublicIPAddress_PS,
            ["New-AzNetworkInterface"] = s_newNetworkInterface_PS,
            ["Get-AzNetworkInterface_Get"] = s_getNetworkInterface_PS,
            ["Get-AzNetworkInterface_List"] = s_listNetworkInterface_PS,
            ["Remove-AzNetworkInterface"] = s_deleteNetworkInterface_PS,
        };

        // Full-path keys `{verb} {path}`; the dispatcher does longest-prefix match on them.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["network vnet create"] = s_newVirtualNetwork_CLI,
            ["network vnet show"] = s_getVirtualNetwork_CLI,
            ["network vnet list"] = s_listVirtualNetwork_CLI,
            ["network vnet delete"] = s_deleteVirtualNetwork_CLI,
            ["network vnet subnet create"] = s_newSubnet_CLI,
            ["network vnet subnet show"] = s_getSubnet_CLI,
            ["network vnet subnet list"] = s_listSubnet_CLI,
            ["network vnet subnet delete"] = s_deleteSubnet_CLI,
            ["network nsg create"] = s_newNetworkSecurityGroup_CLI,
            ["network nsg show"] = s_getNetworkSecurityGroup_CLI,
            ["network nsg list"] = s_listNetworkSecurityGroup_CLI,
            ["network nsg delete"] = s_deleteNetworkSecurityGroup_CLI,
            ["network public-ip create"] = s_newPublicIPAddress_CLI,
            ["network public-ip show"] = s_getPublicIPAddress_CLI,
            ["network public-ip list"] = s_listPublicIPAddress_CLI,
            ["network public-ip delete"] = s_deletePublicIPAddress_CLI,
            ["network nic create"] = s_newNetworkInterface_CLI,
            ["network nic show"] = s_getNetworkInterface_CLI,
            ["network nic list"] = s_listNetworkInterface_CLI,
            ["network nic delete"] = s_deleteNetworkInterface_CLI,
        };

        // Per-processor --profile / -Profile override, injected into every exposed command.
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
