// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts
{
    [ExtensionVerb("login")]
    [ExtensionVerb("logout")]
    [ExtensionVerb("profile")]
    [ExtensionVerb("account")]
    public class ExtensionIndex : IExtensionIndex
    {
        private const string PSCommandPrefix = "Az";

#region PS Command Metadata
        private static readonly CommandMetadata psLogin = new(
            Name: nameof(Processors.LoginProcessor),
            InvocationName: $"Connect-{PSCommandPrefix}Account",
            Aliases: [],
            FullName: typeof(Processors.LoginProcessor).FullName!,
            AssemblyName: typeof(Processors.LoginProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: ["Subscription"], AllowedValues: []),
                ["TenantId"]       = new("TenantId", "TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: ["Tenant"], AllowedValues: []),
                ["UseDeviceCode"]  = new("UseDeviceCode", "UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: ["DeviceCode", "DeviceAuth", "Device", "UseDeviceAuthentication"], AllowedValues: []),
                ["OutProfile"]     = new("OutProfile", "OutProfile", typeof(string), "Create or overwrite a named profile with the login result (mutually exclusive with -Profile).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"]        = new("Profile", "Profile", typeof(string), "Re-authenticate the named existing profile (mutually exclusive with -OutProfile).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Identity"]       = new("Identity", "Identity", typeof(bool), "Login using managed identity in the current environment.", IsRequired: false, Aliases: ["MSI", "ManagedIdentity", "ManagedService"], AllowedValues: []),
                ["ClientId"]       = new("ClientId", "ClientId", typeof(string), "Client ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ObjectId"]       = new("ObjectId", "ObjectId", typeof(string), "Object ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceId"]     = new("ResourceId", "ResourceId", typeof(string), "Resource ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["Subscription"]           = "SubscriptionId",
                ["Tenant"]                 = "TenantId",
                ["DeviceCode"]             = "UseDeviceCode",
                ["DeviceAuth"]             = "UseDeviceCode",
                ["Device"]                 = "UseDeviceCode",
                ["UseDeviceAuthentication"] = "UseDeviceCode",
                ["MSI"]                    = "Identity",
                ["ManagedService"]         = "Identity",
                ["ManagedIdentity"] = "Identity",
            }
        );
        private static readonly CommandMetadata psLogout = new(
            Name: nameof(Processors.LogoutProcessor),
            InvocationName: $"Disconnect-{PSCommandPrefix}Account",
            Aliases: [],
            FullName: typeof(Processors.LogoutProcessor).FullName!,
            AssemblyName: typeof(Processors.LogoutProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata psProfileList = new(
            Name: nameof(Processors.ProfileListProcessor),
            InvocationName: $"Get-{PSCommandPrefix}Profile_List",
            Aliases: [],
            FullName: typeof(Processors.ProfileListProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileListProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata psProfileShow = new(
            Name: nameof(Processors.ProfileShowProcessor),
            InvocationName: $"Get-{PSCommandPrefix}Profile_Show",
            Aliases: [],
            FullName: typeof(Processors.ProfileShowProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileShowProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Active"] = new("Active", "Active", typeof(bool), "Show the active profile. Cannot be used together with Name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata psProfileSet = new(
            Name: nameof(Processors.ProfileSetProcessor),
            InvocationName: $"Set-{PSCommandPrefix}Profile",
            Aliases: [],
            FullName: typeof(Processors.ProfileSetProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileSetProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileSetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The name of the profile to activate.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata psProfileRemove = new(
            Name: nameof(Processors.ProfileRemoveProcessor),
            InvocationName: $"Remove-{PSCommandPrefix}Profile",
            Aliases: [],
            FullName: typeof(Processors.ProfileRemoveProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileRemoveProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileRemoveProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata psGetAccessToken = new(
            Name: nameof(Processors.GetAccessTokenProcessor),
            InvocationName: $"Get-{PSCommandPrefix}AccessToken",
            Aliases: [],
            FullName: typeof(Processors.GetAccessTokenProcessor).FullName!,
            AssemblyName: typeof(Processors.GetAccessTokenProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.GetAccessTokenProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ResourceUrl"]      = new("Resource", "ResourceUrl", typeof(string), "Resource URL (v1.0) to acquire the token for. Mutually exclusive with -ResourceTypeName and -Scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceTypeName"] = new("ResourceType", "ResourceTypeName", typeof(string), "Well-known resource type shortcut (e.g. Arm, MSGraph, KeyVault). Mutually exclusive with -ResourceUrl and -Scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Scope"]            = new("Scope", "Scope", typeof(string[]), "One or more Microsoft Entra v2.0 scopes. Mutually exclusive with -ResourceUrl and -ResourceTypeName.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TenantId"]         = new("Tenant", "TenantId", typeof(string), "Tenant ID to acquire the token for. Defaults to the current profile's tenant.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AsPlainText"]      = new("Plaintext", "AsPlainText", typeof(bool), "Emit the raw access token as plain text. By default the token is redacted in output.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
#endregion

#region CLI Command Metadata
        private static readonly CommandMetadata cliLogin = new(
            Name: nameof(Processors.LoginProcessor),
            InvocationName: "login",
            Aliases: [],
            FullName: typeof(Processors.LoginProcessor).FullName!,
            AssemblyName: typeof(Processors.LoginProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: ["s", "subscription"], AllowedValues: []),
                ["tenant-id"]       = new("TenantId", "tenant-id", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: ["t", "tenant"], AllowedValues: []),
                ["use-device-code"] = new("UseDeviceCode", "use-device-code", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["out-profile"]     = new("OutProfile", "out-profile", typeof(string), "Create or overwrite a named profile with the login result (mutually exclusive with --profile).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["profile"]         = new("Profile", "profile", typeof(string), "Re-authenticate the named existing profile (mutually exclusive with --out-profile).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["identity"]        = new("Identity", "identity", typeof(bool), "Login using managed identity in the current environment.", IsRequired: false, Aliases: ["i"], AllowedValues: []),
                ["client-id"]       = new("ClientId", "client-id", typeof(string), "Client ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["object-id"]       = new("ObjectId", "object-id", typeof(string), "Object ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-id"]     = new("ResourceId", "resource-id", typeof(string), "Resource ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["s"]            = "subscription-id",
                ["subscription"] = "subscription-id",
                ["t"]            = "tenant-id",
                ["tenant"]       = "tenant-id",
                ["i"]            = "identity",
            }
        );
        private static readonly CommandMetadata cliLogout = new(
            Name: nameof(Processors.LogoutProcessor),
            InvocationName: "logout",
            Aliases: [],
            FullName: typeof(Processors.LogoutProcessor).FullName!,
            AssemblyName: typeof(Processors.LogoutProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliProfileList = new(
            Name: nameof(Processors.ProfileListProcessor),
            InvocationName: "profile list",
            Aliases: [],
            FullName: typeof(Processors.ProfileListProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileListProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliProfileShow = new(
            Name: nameof(Processors.ProfileShowProcessor),
            InvocationName: "profile show",
            Aliases: [],
            FullName: typeof(Processors.ProfileShowProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileShowProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                // The profile name is accepted positionally (`profile show <name>`) or via --name (aliased to the positional slot).
                ["name"] = new("Name", "name", typeof(string), "The name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["active"] = new("Active", "active", typeof(bool), "Show the active profile. Cannot be used together with the profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { [""] = "name" }
        );
        private static readonly CommandMetadata cliProfileSet = new(
            Name: nameof(Processors.ProfileSetProcessor),
            InvocationName: "profile set",
            Aliases: [],
            FullName: typeof(Processors.ProfileSetProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileSetProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileSetProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the profile to activate.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { [""] = "name" }
        );
        private static readonly CommandMetadata cliProfileRemove = new(
            Name: nameof(Processors.ProfileRemoveProcessor),
            InvocationName: "profile remove",
            Aliases: [],
            FullName: typeof(Processors.ProfileRemoveProcessor).FullName!,
            AssemblyName: typeof(Processors.ProfileRemoveProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ProfileRemoveProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { [""] = "name" }
        );
        private static readonly CommandMetadata cliGetAccessToken = new(
            Name: nameof(Processors.GetAccessTokenProcessor),
            InvocationName: "account get-access-token",
            Aliases: [],
            FullName: typeof(Processors.GetAccessTokenProcessor).FullName!,
            AssemblyName: typeof(Processors.GetAccessTokenProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.GetAccessTokenProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["resource"]      = new("Resource", "resource", typeof(string), "Resource URL (v1.0) to acquire the token for. Mutually exclusive with --resource-type and --scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-type"] = new("ResourceType", "resource-type", typeof(string), "Well-known resource type shortcut (e.g. arm, ms-graph, key-vault). Mutually exclusive with --resource and --scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["scope"]         = new("Scope", "scope", typeof(string[]), "One or more Microsoft Entra v2.0 scopes. Mutually exclusive with --resource and --resource-type.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tenant-id"]     = new("Tenant", "tenant-id", typeof(string), "Tenant ID to acquire the token for. Defaults to the current profile's tenant.", IsRequired: false, Aliases: ["t", "tenant"], AllowedValues: []),
                ["plaintext"]     = new("Plaintext", "plaintext", typeof(bool), "Emit the raw access token as plain text. By default the token is redacted in output.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["t"] = "tenant-id",
                ["tenant"] = "tenant-id",
            }
        );
#endregion

#region Command Tables
        private static readonly Dictionary<string, CommandMetadata> psTable = BuildCommandTable(
            psLogin,
            psLogout,
            psProfileList,
            psProfileShow,
            psProfileSet,
            psProfileRemove,
            psGetAccessToken
        );
        private static readonly Dictionary<string, CommandMetadata> cliTable = BuildCommandTable(
            cliLogin,
            cliLogout,
            cliProfileList,
            cliProfileShow,
            cliProfileSet,
            cliProfileRemove,
            cliGetAccessToken
        );
        private static Dictionary<string, CommandMetadata> BuildCommandTable(params CommandMetadata[] commands) =>
            commands.ToDictionary(cmd => cmd.InvocationName, cmd => cmd);
#endregion

#region Interface Implementation
        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            return GetCommandTable(clientType).TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => psTable,
                ClientType.CLI => cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => psTable.Keys,
                ClientType.CLI => cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return GetCommandTable(clientType).Values;
        }
    }
#endregion
}
