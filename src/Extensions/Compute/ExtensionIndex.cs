// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Compute
{
    [ExtensionVerb("vm")]
    [ExtensionVerb("disk")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newVM_PS = new(
            Name: "NewVMProcessor",
            InvocationName: "New-AzVM",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]         = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                   = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]               = new("Location", "Location", typeof(string), "Location for the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]      = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["VmSize"]                 = new("VmSize", "VmSize", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminUsername"]          = new("AdminUsername", "AdminUsername", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminPassword"]          = new("AdminPassword", "AdminPassword", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["NetworkInterfaceId"]     = new("NetworkInterfaceId", "NetworkInterfaceId", typeof(string), "Resource id of the network interface to attach.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["OsDiskCreateOption"]     = new("OsDiskCreateOption", "OsDiskCreateOption", typeof(string), "OS disk create option.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImagePublisher"]         = new("ImagePublisher", "ImagePublisher", typeof(string), "Image publisher.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageOffer"]             = new("ImageOffer", "ImageOffer", typeof(string), "Image offer.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageSku"]               = new("ImageSku", "ImageSku", typeof(string), "Image SKU.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageVersion"]           = new("ImageVersion", "ImageVersion", typeof(string), "Image version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SecurityType"]           = new("SecurityType", "SecurityType", typeof(string), "Security type (e.g. trustedlaunch).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SecureBootEnabled"]      = new("SecureBootEnabled", "SecureBootEnabled", typeof(bool), "Whether secure boot is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VTpmEnabled"]            = new("VTpmEnabled", "VTpmEnabled", typeof(bool), "Whether vTPM is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["BootDiagnosticsEnabled"] = new("BootDiagnosticsEnabled", "BootDiagnosticsEnabled", typeof(bool), "Whether boot diagnostics is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getVM_PS = new(
            Name: "GetVMProcessor",
            InvocationName: "Get-AzVM_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listVM_PS = new(
            Name: "ListVMProcessor",
            InvocationName: "Get-AzVM_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.ListVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteVM_PS = new(
            Name: "DeleteVMProcessor",
            InvocationName: "Remove-AzVM",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.DeleteVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.DeleteVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_startVM_PS = new(
            Name: "StartVMProcessor_Orchestrator",
            InvocationName: "Start-AzVM",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.StartVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.StartVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Id"]                = new("Ids", "Id", typeof(string[]), "The resource ID of the virtual machine. Provide either -Id or -Name and -ResourceGroupName.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVMOrchestrator_PS = new(
            Name: "NewVMOrchestrator",
            InvocationName: "New-AzVM",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]                 = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                           = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                       = new("Location", "Location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]              = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["VmSize"]                         = new("VmSize", "VmSize", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminUsername"]                  = new("AdminUsername", "AdminUsername", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdminPassword"]                  = new("AdminPassword", "AdminPassword", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["NetworkInterfaceName"]           = new("NetworkInterfaceName", "NetworkInterfaceName", typeof(string), "The network interface name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VirtualNetworkName"]             = new("VirtualNetworkName", "VirtualNetworkName", typeof(string), "The virtual network name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkSecurityGroupName"]       = new("NetworkSecurityGroupName", "NetworkSecurityGroupName", typeof(string), "The network security group name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicIPAddressId"]              = new("PublicIPAddressId", "PublicIPAddressId", typeof(string), "Resource id of an existing public IP address to attach.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicIPAddressName"]            = new("PublicIPAddressName", "PublicIPAddressName", typeof(string), "The public IP address name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicIPAddressDomainNameLabel"] = new("PublicIPAddressDomainNameLabel", "PublicIPAddressDomainNameLabel", typeof(string), "DNS domain name label for the public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicIPAddressSku"]             = new("PublicIPAddressSku", "PublicIPAddressSku", typeof(string), "SKU for an orchestrated public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicIPAddressAllocationMethod"] = new("PublicIPAddressAllocationMethod", "PublicIPAddressAllocationMethod", typeof(string), "Allocation method for an orchestrated public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NoPublicIPAddress"]              = new("NoPublicIPAddress", "NoPublicIPAddress", typeof(bool), "Create the VM without attaching a public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableAcceleratedNetworking"]    = new("EnableAcceleratedNetworking", "EnableAcceleratedNetworking", typeof(bool), "Enable accelerated networking on the network interface.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DisableAcceleratedNetworking"]   = new("DisableAcceleratedNetworking", "DisableAcceleratedNetworking", typeof(bool), "Disable accelerated networking on the network interface.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetNames"]                    = new("SubnetNames", "SubnetNames", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubnetAddressPrefixes"]          = new("SubnetAddressPrefixes", "SubnetAddressPrefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AddressPrefixes"]                = new("AddressPrefixes", "AddressPrefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleNames"]                      = new("RuleNames", "RuleNames", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDirections"]                 = new("RuleDirections", "RuleDirections", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleProtocols"]                  = new("RuleProtocols", "RuleProtocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RulePriorities"]                 = new("RulePriorities", "RulePriorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourceAddressPrefixes"]      = new("RuleSourceAddressPrefixes", "RuleSourceAddressPrefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleSourcePortRanges"]           = new("RuleSourcePortRanges", "RuleSourcePortRanges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationPortRanges"]      = new("RuleDestinationPortRanges", "RuleDestinationPortRanges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleDestinationAddressPrefixes"] = new("RuleDestinationAddressPrefixes", "RuleDestinationAddressPrefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RuleAccesses"]                   = new("RuleAccesses", "RuleAccesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["OsDiskCreateOption"]             = new("OsDiskCreateOption", "OsDiskCreateOption", typeof(string), "OS disk create option.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageId"]                         = new("ImageId", "ImageId", typeof(string), "Resource id of a custom image to use.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImagePublisher"]                  = new("ImagePublisher", "ImagePublisher", typeof(string), "Image publisher.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageOffer"]                      = new("ImageOffer", "ImageOffer", typeof(string), "Image offer.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageSku"]                        = new("ImageSku", "ImageSku", typeof(string), "Image SKU.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImageVersion"]                    = new("ImageVersion", "ImageVersion", typeof(string), "Image version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SecurityType"]                    = new("SecurityType", "SecurityType", typeof(string), "Security type for the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SecureBootEnabled"]               = new("SecureBootEnabled", "SecureBootEnabled", typeof(bool), "Whether secure boot is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VTpmEnabled"]                     = new("VTpmEnabled", "VTpmEnabled", typeof(bool), "Whether vTPM is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["BootDiagnosticsEnabled"]          = new("BootDiagnosticsEnabled", "BootDiagnosticsEnabled", typeof(bool), "Whether boot diagnostics is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DisableBootDiagnostics"]          = new("DisableBootDiagnostics", "DisableBootDiagnostics", typeof(bool), "Disable boot diagnostics.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["BootDiagnosticsStorageUri"]       = new("BootDiagnosticsStorageUri", "BootDiagnosticsStorageUri", typeof(string), "Storage URI for boot diagnostics.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["BootDiagnosticsStorageAccountUri"] = new("BootDiagnosticsStorageAccountUri", "BootDiagnosticsStorageAccountUri", typeof(string), "Storage account endpoint for boot diagnostics.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IdentityType"]                    = new("IdentityType", "IdentityType", typeof(string), "Managed identity type for the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SystemAssignedIdentity"]          = new("SystemAssignedIdentity", "SystemAssignedIdentity", typeof(bool), "Enable a system-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UserAssignedIdentityIds"]         = new("UserAssignedIdentityIds", "UserAssignedIdentityIds", typeof(List<string>), "User-assigned managed identity resource IDs.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newVirtualNetwork_CLI = new(
            Name: "NewVirtualNetworkProcessor",
            InvocationName: "vm vnet create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewVirtualNetworkProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVirtualNetworkProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                    = new("Name", "name", typeof(string), "The name of the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                = new("Location", "location", typeof(string), "Location for the virtual network.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-names"]            = new("SubnetNames", "subnet-names", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"] = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["address-prefixes"]        = new("AddressPrefixes", "address-prefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkSecurityGroup_CLI = new(
            Name: "NewNetworkSecurityGroupProcessor",
            InvocationName: "vm nsg create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewNetworkSecurityGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkSecurityGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                             = new("Name", "name", typeof(string), "The name of the network security group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                         = new("Location", "location", typeof(string), "Location for the network security group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]                   = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["rule-names"]                       = new("RuleNames", "rule-names", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-directions"]                  = new("RuleDirections", "rule-directions", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-protocols"]                   = new("RuleProtocols", "rule-protocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-priorities"]                  = new("RulePriorities", "rule-priorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-address-prefixes"]     = new("RuleSourceAddressPrefixes", "rule-source-address-prefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-port-ranges"]          = new("RuleSourcePortRanges", "rule-source-port-ranges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-port-ranges"]     = new("RuleDestinationPortRanges", "rule-destination-port-ranges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-address-prefixes"]= new("RuleDestinationAddressPrefixes", "rule-destination-address-prefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-accesses"]                    = new("RuleAccesses", "rule-accesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkInterface_CLI = new(
            Name: "NewNetworkInterfaceProcessor",
            InvocationName: "vm nic create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewNetworkInterfaceProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]            = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                       = new("Name", "name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                   = new("Location", "location", typeof(string), "Location for the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]             = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-ids"]                 = new("SubnetIds", "subnet-ids", typeof(List<string>), "Ids of the subnets to bind IP configurations to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"]    = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group-id"]  = new("NetworkSecurityGroupId", "network-security-group-id", typeof(string), "Resource id of the network security group to associate.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVM_CLI = new(
            Name: "NewVMProcessor",
            InvocationName: "vm create-basic",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]          = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                     = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                 = new("Location", "location", typeof(string), "Location for the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]           = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["vm-size"]                  = new("VmSize", "vm-size", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-username"]           = new("AdminUsername", "admin-username", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-password"]           = new("AdminPassword", "admin-password", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-interface-id"]     = new("NetworkInterfaceId", "network-interface-id", typeof(string), "Resource id of the network interface to attach.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["os-disk-create-option"]    = new("OsDiskCreateOption", "os-disk-create-option", typeof(string), "OS disk create option.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-publisher"]          = new("ImagePublisher", "image-publisher", typeof(string), "Image publisher.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-offer"]              = new("ImageOffer", "image-offer", typeof(string), "Image offer.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-sku"]                = new("ImageSku", "image-sku", typeof(string), "Image SKU.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-version"]            = new("ImageVersion", "image-version", typeof(string), "Image version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["security-type"]            = new("SecurityType", "security-type", typeof(string), "Security type (e.g. trustedlaunch).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["secure-boot-enabled"]      = new("SecureBootEnabled", "secure-boot-enabled", typeof(bool), "Whether secure boot is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["vtpm-enabled"]             = new("VTpmEnabled", "vtpm-enabled", typeof(bool), "Whether vTPM is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["boot-diagnostics-enabled"] = new("BootDiagnosticsEnabled", "boot-diagnostics-enabled", typeof(bool), "Whether boot diagnostics is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getVM_CLI = new(
            Name: "GetVMProcessor",
            InvocationName: "vm show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listVM_CLI = new(
            Name: "ListVMProcessor",
            InvocationName: "vm list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.ListVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteVM_CLI = new(
            Name: "DeleteVMProcessor",
            InvocationName: "vm delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.DeleteVMProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.DeleteVMProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_startVM_CLI = new(
            Name: "StartVMProcessor_Orchestrator",
            InvocationName: "vm start",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.StartVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.StartVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ids"]             = new("Ids", "ids", typeof(string[]), "One or more virtual machine resource IDs. Provide either --ids or --name and --resource-group.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // Lives under the `disk` verb declared by the class-level [ExtensionVerb("disk")]
        // attribute — proves one extension can contribute to multiple top-level namespaces.
        private static readonly CommandMetadata s_listDisk_CLI = new(
            Name: "ListDiskProcessor",
            InvocationName: "disk list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.ListDiskProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.ListDiskProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newNetworkInterfaceOrchestrator_CLI = new(
            Name: "NewNetworkInterfaceProcessor_Orchestrator",
            InvocationName: "vm nic create-orchestrated",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewNetworkInterfaceProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewNetworkInterfaceProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                             = new("Name", "name", typeof(string), "The name of the network interface.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                         = new("Location", "location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]                   = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["virtual-network-name"]             = new("VirtualNetworkName", "virtual-network-name", typeof(string), "The virtual network name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-security-group-name"]      = new("NetworkSecurityGroupName", "network-security-group-name", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subnet-names"]                     = new("SubnetNames", "subnet-names", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"]          = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["address-prefixes"]                 = new("AddressPrefixes", "address-prefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-names"]                       = new("RuleNames", "rule-names", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-directions"]                  = new("RuleDirections", "rule-directions", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-protocols"]                   = new("RuleProtocols", "rule-protocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-priorities"]                  = new("RulePriorities", "rule-priorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-address-prefixes"]     = new("RuleSourceAddressPrefixes", "rule-source-address-prefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-port-ranges"]          = new("RuleSourcePortRanges", "rule-source-port-ranges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-port-ranges"]     = new("RuleDestinationPortRanges", "rule-destination-port-ranges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-address-prefixes"]= new("RuleDestinationAddressPrefixes", "rule-destination-address-prefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-accesses"]                    = new("RuleAccesses", "rule-accesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newVMOrchestrator_CLI = new(
            Name: "NewVMProcessor_Orchestrator",
            InvocationName: "vm create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.NewVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]                  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                             = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]                         = new("Location", "location", typeof(string), "Location for all resources.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]                   = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["vm-size"]                          = new("VmSize", "vm-size", typeof(string), "The size of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-username"]                   = new("AdminUsername", "admin-username", typeof(string), "The administrator username.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["admin-password"]                   = new("AdminPassword", "admin-password", typeof(string), "The administrator password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["network-interface-name"]           = new("NetworkInterfaceName", "network-interface-name", typeof(string), "The network interface name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["virtual-network-name"]             = new("VirtualNetworkName", "virtual-network-name", typeof(string), "The virtual network name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-security-group-name"]      = new("NetworkSecurityGroupName", "network-security-group-name", typeof(string), "The network security group name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address-id"]             = new("PublicIPAddressId", "public-ip-address-id", typeof(string), "Resource id of an existing public IP address to attach.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address-name"]           = new("PublicIPAddressName", "public-ip-address-name", typeof(string), "The public IP address name to create.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address-domain-name-label"] = new("PublicIPAddressDomainNameLabel", "public-ip-address-domain-name-label", typeof(string), "DNS domain name label for the public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address-sku"]            = new("PublicIPAddressSku", "public-ip-address-sku", typeof(string), "SKU for an orchestrated public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-ip-address-allocation-method"] = new("PublicIPAddressAllocationMethod", "public-ip-address-allocation-method", typeof(string), "Allocation method for an orchestrated public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["no-public-ip-address"]             = new("NoPublicIPAddress", "no-public-ip-address", typeof(bool), "Create the VM without attaching a public IP address.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-accelerated-networking"]    = new("EnableAcceleratedNetworking", "enable-accelerated-networking", typeof(bool), "Enable accelerated networking on the network interface.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["disable-accelerated-networking"]   = new("DisableAcceleratedNetworking", "disable-accelerated-networking", typeof(bool), "Disable accelerated networking on the network interface.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-names"]                     = new("SubnetNames", "subnet-names", typeof(List<string>), "Names of the subnets.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subnet-address-prefixes"]          = new("SubnetAddressPrefixes", "subnet-address-prefixes", typeof(List<List<string>>), "Address prefixes for each subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["address-prefixes"]                 = new("AddressPrefixes", "address-prefixes", typeof(List<string>), "Address prefixes for the virtual network.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-names"]                       = new("RuleNames", "rule-names", typeof(List<string>), "Names of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-directions"]                  = new("RuleDirections", "rule-directions", typeof(List<string>), "Directions of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-protocols"]                   = new("RuleProtocols", "rule-protocols", typeof(List<string>), "Protocols of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-priorities"]                  = new("RulePriorities", "rule-priorities", typeof(List<int>), "Priorities of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-address-prefixes"]     = new("RuleSourceAddressPrefixes", "rule-source-address-prefixes", typeof(List<string>), "Source address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-source-port-ranges"]          = new("RuleSourcePortRanges", "rule-source-port-ranges", typeof(List<string>), "Source port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-port-ranges"]     = new("RuleDestinationPortRanges", "rule-destination-port-ranges", typeof(List<string>), "Destination port ranges of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-destination-address-prefixes"]= new("RuleDestinationAddressPrefixes", "rule-destination-address-prefixes", typeof(List<string>), "Destination address prefixes of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rule-accesses"]                    = new("RuleAccesses", "rule-accesses", typeof(List<string>), "Access types (Allow/Deny) of the security rules.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["os-disk-create-option"]            = new("OsDiskCreateOption", "os-disk-create-option", typeof(string), "OS disk create option.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-id"]                         = new("ImageId", "image-id", typeof(string), "Resource id of a custom image to use.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-publisher"]                  = new("ImagePublisher", "image-publisher", typeof(string), "Image publisher.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-offer"]                      = new("ImageOffer", "image-offer", typeof(string), "Image offer.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-sku"]                        = new("ImageSku", "image-sku", typeof(string), "Image SKU.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["image-version"]                    = new("ImageVersion", "image-version", typeof(string), "Image version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["security-type"]                    = new("SecurityType", "security-type", typeof(string), "Security type for the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["secure-boot-enabled"]              = new("SecureBootEnabled", "secure-boot-enabled", typeof(bool), "Whether secure boot is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["vtpm-enabled"]                     = new("VTpmEnabled", "vtpm-enabled", typeof(bool), "Whether vTPM is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["boot-diagnostics-enabled"]         = new("BootDiagnosticsEnabled", "boot-diagnostics-enabled", typeof(bool), "Whether boot diagnostics is enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["disable-boot-diagnostics"]         = new("DisableBootDiagnostics", "disable-boot-diagnostics", typeof(bool), "Disable boot diagnostics.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["boot-diagnostics-storage-uri"]     = new("BootDiagnosticsStorageUri", "boot-diagnostics-storage-uri", typeof(string), "Storage URI for boot diagnostics.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["boot-diagnostics-storage-account-uri"] = new("BootDiagnosticsStorageAccountUri", "boot-diagnostics-storage-account-uri", typeof(string), "Storage account endpoint for boot diagnostics.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["identity-type"]                    = new("IdentityType", "identity-type", typeof(string), "Managed identity type for the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["system-assigned-identity"]         = new("SystemAssignedIdentity", "system-assigned-identity", typeof(bool), "Enable a system-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["user-assigned-identity-ids"]       = new("UserAssignedIdentityIds", "user-assigned-identity-ids", typeof(List<string>), "User-assigned managed identity resource IDs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-integrity-monitoring"]      = new("EnableIntegrityMonitoring", "enable-integrity-monitoring", typeof(bool), "Create the guest attestation extension for integrity monitoring after VM creation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["disable-integrity-monitoring-auto-upgrade"] = new("DisableIntegrityMonitoringAutoUpgrade", "disable-integrity-monitoring-auto-upgrade", typeof(bool), "Disable automatic upgrade on the guest attestation extension.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            // ["New-AzVM"]                           = s_newVM_PS,
            ["New-AzVM"]                   = s_newVMOrchestrator_PS,
            ["Get-AzVM_Get"]                   = s_getVM_PS,
            ["Get-AzVM_List"]               = s_listVM_PS,
            ["Remove-AzVM"]                = s_deleteVM_PS,
            ["Start-AzVM"]                 = s_startVM_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["vm create"]   = s_newVMOrchestrator_CLI,
            ["vm show"]     = s_getVM_CLI,
            ["vm list"]     = s_listVM_CLI,
            ["vm delete"]   = s_deleteVM_CLI,
            ["vm start"]    = s_startVM_CLI,
            ["disk list"]   = s_listDisk_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Compute processors accept the parameter; the Engine
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
