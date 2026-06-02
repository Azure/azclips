// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Compute
{
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_PS = new(
            Name: "NewVirtualNetworkProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]        = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                  = new("Name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]              = new("Location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]     = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubnetNames"]           = new("SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"] = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AddressPrefixes"]       = new("AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkSecurityGroup_PS = new(
            Name: "NewNetworkSecurityGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]                 = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                           = new("Name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                       = new("Location", typeof(string), "Location for the network security group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]              = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["RuleNames"]                      = new("RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDirections"]                 = new("RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleProtocols"]                  = new("RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RulePriorities"]                 = new("RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourceAddressPrefixes"]      = new("RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourcePortRanges"]           = new("RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationPortRanges"]      = new("RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationAddressPrefixes"] = new("RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleAccesses"]                   = new("RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkInterface_PS = new(
            Name: "NewNetworkInterfaceProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]          = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                    = new("Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                = new("Location", typeof(string), "Location for the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]       = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubnetIds"]             = new("SubnetIds", typeof(List<string>), "Ids of the subnets to bind IP configurations to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]   = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupId"]  = new("NetworkSecurityGroupId", typeof(string), "Resource id of the network security group to associate.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVM_PS = new(
            Name: "NewVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]         = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                   = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]               = new("Location", typeof(string), "Location for the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]      = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["VmSize"]                 = new("VmSize", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminUsername"]          = new("AdminUsername", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminPassword"]          = new("AdminPassword", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["NetworkInterfaceId"]     = new("NetworkInterfaceId", typeof(string), "Resource id of the network interface to attach.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["OsDiskCreateOption"]     = new("OsDiskCreateOption", typeof(string), "OS disk create option.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImagePublisher"]         = new("ImagePublisher", typeof(string), "Image publisher.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageOffer"]             = new("ImageOffer", typeof(string), "Image offer.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageSku"]               = new("ImageSku", typeof(string), "Image SKU.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageVersion"]           = new("ImageVersion", typeof(string), "Image version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SecurityType"]           = new("SecurityType", typeof(string), "Security type (e.g. trustedlaunch).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SecureBootEnabled"]      = new("SecureBootEnabled", typeof(bool), "Whether secure boot is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VTpmEnabled"]            = new("VTpmEnabled", typeof(bool), "Whether vTPM is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["BootDiagnosticsEnabled"] = new("BootDiagnosticsEnabled", typeof(bool), "Whether boot diagnostics is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getVM_PS = new(
            Name: "GetVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.GetVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listVM_PS = new(
            Name: "ListVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.ListVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteVM_PS = new(
            Name: "DeleteVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.DeleteVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.DeleteVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkInterfaceOrchestrator_PS = new(
            Name: "NewNetworkInterfaceProcessor_Orchestrator",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]                 = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                           = new("Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                       = new("Location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]              = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["VirtualNetworkName"]             = new("VirtualNetworkName", typeof(string), "The virtual network name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupName"]       = new("NetworkSecurityGroupName", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubnetNames"]                    = new("SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]          = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AddressPrefixes"]                = new("AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleNames"]                      = new("RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDirections"]                 = new("RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleProtocols"]                  = new("RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RulePriorities"]                 = new("RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourceAddressPrefixes"]      = new("RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourcePortRanges"]           = new("RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationPortRanges"]      = new("RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationAddressPrefixes"] = new("RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleAccesses"]                   = new("RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVMOrchestrator_PS = new(
            Name: "NewVMOrchestrator",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]                 = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                           = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                       = new("Location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]              = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["VmSize"]                         = new("VmSize", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminUsername"]                  = new("AdminUsername", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminPassword"]                  = new("AdminPassword", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["NetworkInterfaceName"]           = new("NetworkInterfaceName", typeof(string), "The network interface name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["VirtualNetworkName"]             = new("VirtualNetworkName", typeof(string), "The virtual network name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupName"]       = new("NetworkSecurityGroupName", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubnetNames"]                    = new("SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]          = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AddressPrefixes"]                = new("AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleNames"]                      = new("RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDirections"]                 = new("RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleProtocols"]                  = new("RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RulePriorities"]                 = new("RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourceAddressPrefixes"]      = new("RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourcePortRanges"]           = new("RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationPortRanges"]      = new("RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationAddressPrefixes"] = new("RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleAccesses"]                   = new("RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_CLI = new(
            Name: "NewVirtualNetworkProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                    = new("Name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                = new("Location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-names"]            = new("SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"] = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["address-prefixes"]        = new("AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkSecurityGroup_CLI = new(
            Name: "NewNetworkSecurityGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                  = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                             = new("Name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                         = new("Location", typeof(string), "Location for the network security group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]                   = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["rule-names"]                       = new("RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-directions"]                  = new("RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-protocols"]                   = new("RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-priorities"]                  = new("RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-address-prefixes"]     = new("RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-port-ranges"]          = new("RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-port-ranges"]     = new("RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-address-prefixes"]= new("RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-accesses"]                    = new("RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkInterface_CLI = new(
            Name: "NewNetworkInterfaceProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]            = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                       = new("Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                   = new("Location", typeof(string), "Location for the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]             = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-ids"]                 = new("SubnetIds", typeof(List<string>), "Ids of the subnets to bind IP configurations to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"]    = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group-id"]  = new("NetworkSecurityGroupId", typeof(string), "Resource id of the network security group to associate.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVM_CLI = new(
            Name: "NewVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]          = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                     = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                 = new("Location", typeof(string), "Location for the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]           = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["vm-size"]                  = new("VmSize", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-username"]           = new("AdminUsername", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-password"]           = new("AdminPassword", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-interface-id"]     = new("NetworkInterfaceId", typeof(string), "Resource id of the network interface to attach.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["os-disk-create-option"]    = new("OsDiskCreateOption", typeof(string), "OS disk create option.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-publisher"]          = new("ImagePublisher", typeof(string), "Image publisher.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-offer"]              = new("ImageOffer", typeof(string), "Image offer.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-sku"]                = new("ImageSku", typeof(string), "Image SKU.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-version"]            = new("ImageVersion", typeof(string), "Image version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["security-type"]            = new("SecurityType", typeof(string), "Security type (e.g. trustedlaunch).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["secure-boot-enabled"]      = new("SecureBootEnabled", typeof(bool), "Whether secure boot is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["vtpm-enabled"]             = new("VTpmEnabled", typeof(bool), "Whether vTPM is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["boot-diagnostics-enabled"] = new("BootDiagnosticsEnabled", typeof(bool), "Whether boot diagnostics is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getVM_CLI = new(
            Name: "GetVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.GetVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listVM_CLI = new(
            Name: "ListVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.ListVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteVM_CLI = new(
            Name: "DeleteVMProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.DeleteVMProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.DeleteVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkInterfaceOrchestrator_CLI = new(
            Name: "NewNetworkInterfaceProcessor_Orchestrator",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                  = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                             = new("Name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                         = new("Location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]                   = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["virtual-network-name"]             = new("VirtualNetworkName", typeof(string), "The virtual network name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-security-group-name"]      = new("NetworkSecurityGroupName", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-names"]                     = new("SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"]          = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["address-prefixes"]                 = new("AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-names"]                       = new("RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-directions"]                  = new("RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-protocols"]                   = new("RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-priorities"]                  = new("RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-address-prefixes"]     = new("RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-port-ranges"]          = new("RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-port-ranges"]     = new("RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-address-prefixes"]= new("RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-accesses"]                    = new("RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVMOrchestrator_CLI = new(
            Name: "NewVMProcessor_Orchestrator",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Compute.Processors.NewVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                  = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]                             = new("Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                         = new("Location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]                   = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["vm-size"]                          = new("VmSize", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-username"]                   = new("AdminUsername", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-password"]                   = new("AdminPassword", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-interface-name"]           = new("NetworkInterfaceName", typeof(string), "The network interface name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["virtual-network-name"]             = new("VirtualNetworkName", typeof(string), "The virtual network name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-security-group-name"]      = new("NetworkSecurityGroupName", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-names"]                     = new("SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"]          = new("SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["address-prefixes"]                 = new("AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-names"]                       = new("RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-directions"]                  = new("RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-protocols"]                   = new("RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-priorities"]                  = new("RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-address-prefixes"]     = new("RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-port-ranges"]          = new("RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-port-ranges"]     = new("RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-address-prefixes"]= new("RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-accesses"]                    = new("RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            // ["New-VirtualNetwork"]               = s_newVirtualNetwork_PS,
            // ["New-NetworkSecurityGroup"]         = s_newNetworkSecurityGroup_PS,
            // ["New-NetworkInterface"]             = s_newNetworkInterface_PS,
            // ["New-VM"]                           = s_newVM_PS,
            // ["New-NetworkInterfaceOrchestrator"]     = s_newNetworkInterfaceOrchestrator_PS,
            ["New-VM"]                   = s_newVMOrchestrator_PS,
            ["Get-VM_Get"]                   = s_getVM_PS,
            ["Get-VM_List"]               = s_listVM_PS,
            ["Remove-VM"]                = s_deleteVM_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["create"]   = s_newVMOrchestrator_CLI,
            ["show"]     = s_getVM_CLI,
            ["list"]     = s_listVM_CLI,
            ["delete"]   = s_deleteVM_CLI,
        };
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
    }
}
