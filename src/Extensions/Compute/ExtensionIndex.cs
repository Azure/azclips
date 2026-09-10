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
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newVM_CLI = new(
            Name: "NewVMProcessor",
            InvocationName: "vm create",
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
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzVM"]                   = s_newVM_PS,
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
            ["vm create"]   = s_newVM_CLI,
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
