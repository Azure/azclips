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
        private static readonly string[] s_encryptionPolicies = ["DropUnencrypted", "AllowUnencrypted"];
        private static readonly string[] s_peVnetPolicies = ["Disabled", "Basic"];

        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_PS = new(
            Name: "NewVirtualNetworkProcessor",
            InvocationName: "New-AzVirtualNetwork",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]              = new("SubscriptionId", "SubscriptionId", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                        = new("Name", "Name", typeof(string), NameHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                    = new("Location", "Location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]           = new("ResourceGroupName", "ResourceGroupName", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: [], AllowedValues: []),
                ["AddressPrefix"]               = new("AddressPrefixes", "AddressPrefix", typeof(List<string>), AddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Subnet"]                      = new("Subnets", "Subnet", typeof(List<List<string>>), SubnetsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetNames"]                 = new("SubnetNames", "SubnetNames", typeof(List<string>), SubnetNamesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]       = new("SubnetAddressPrefixes", "SubnetAddressPrefixes", typeof(List<List<string>>), SubnetAddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsServer"]                   = new("DnsServers", "DnsServer", typeof(List<string>), DnsServersHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tag"]                         = new("Tags", "Tag", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
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

        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_CLI = new(
            Name: "NewVirtualNetworkProcessor",
            InvocationName: "network vnet create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Network.Processors.NewVirtualNetworkProcessor",
            AssemblyName: Assembly,
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", "subscription-id", typeof(string), SubscriptionHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                    = new("Name", "name", typeof(string), NameHelp, IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["location"]                = new("Location", "location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", "resource-group", typeof(string), ResourceGroupHelp, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["address-prefixes"]        = new("AddressPrefixes", "address-prefixes", typeof(List<string>), AddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnets"]                 = new("Subnets", "subnets", typeof(List<List<string>>), SubnetsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-names"]            = new("SubnetNames", "subnet-names", typeof(List<string>), SubnetNamesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"] = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), SubnetAddressPrefixesHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-servers"]             = new("DnsServers", "dns-servers", typeof(List<string>), DnsServersHelp, IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"]                    = new("Tags", "tags", typeof(List<string>), TagsHelp, IsRequired: false, Aliases: [], AllowedValues: []),
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

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzVirtualNetwork"]      = s_newVirtualNetwork_PS,
            ["Get-AzVirtualNetwork_Get"]  = s_getVirtualNetwork_PS,
            ["Get-AzVirtualNetwork_List"] = s_listVirtualNetwork_PS,
            ["Remove-AzVirtualNetwork"]   = s_deleteVirtualNetwork_PS,
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
