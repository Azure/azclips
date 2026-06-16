// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Login
{
    [ExtensionVerb("login")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_login_PS = new(
            Name: "LoginProcessor",
            InvocationName: "Connect-AzAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("Subscription", "SubscriptionId", typeof(string), "Azure Subscription ID. If omitted, a default subscription is auto-selected.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TenantId"]       = new("Tenant", "TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UseDeviceCode"]  = new("UseDeviceCode", "UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Identity"]             = new("Identity", "Identity", typeof(bool), "Login using managed identity in the current environment.", IsRequired: false, Aliases: ["MSI", "ManagedIdentity"], AllowedValues: []),
                ["ClientId"]             = new("ClientId", "ClientId", typeof(string), "Client ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ObjectId"]             = new("ObjectId", "ObjectId", typeof(string), "Object ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceId"]           = new("ResourceId", "ResourceId", typeof(string), "Resource ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["MSI"] = "Identity",
                ["ManagedIdentity"] = "Identity",
            }
        );
        private static readonly CommandMetadata s_logout_PS = new(
            Name: "LogoutProcessor",
            InvocationName: "Disconnect-AzAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getContext_PS = new(
            Name: "GetContextProcessor",
            InvocationName: "Get-AzAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.GetContextProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.GetContextProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_login_CLI = new(
            Name: "LoginProcessor",
            InvocationName: "login",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("Subscription", "subscription-id", typeof(string), "Azure Subscription ID. If omitted, a default subscription is auto-selected.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tenant-id"]       = new("Tenant", "tenant-id", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["use-device-code"] = new("UseDeviceCode", "use-device-code", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["identity"]              = new("Identity", "identity", typeof(bool), "Login using managed identity in the current environment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-id"]             = new("ClientId", "client-id", typeof(string), "Client ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["object-id"]             = new("ObjectId", "object-id", typeof(string), "Object ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-id"]           = new("ResourceId", "resource-id", typeof(string), "Resource ID of the user-assigned managed identity.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_CLI = new(
            Name: "LogoutProcessor",
            InvocationName: "login logout",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getContext_CLI = new(
            Name: "GetContextProcessor",
            InvocationName: "login show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.GetContextProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.GetContextProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Connect-AzAccount"]    = s_login_PS,
            ["Disconnect-AzAccount"] = s_logout_PS,
            ["Get-AzAccount"]        = s_getContext_PS,
        };
        // Full-path keys. The root verb (`az login`) keys as just "login";
        // sub-leaves are `login {path}`.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["login"]        = s_login_CLI,
            ["login logout"] = s_logout_CLI,
            ["login show"]   = s_getContext_CLI,
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
        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Keys,
                ClientType.CLI => s_cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return GetCommandTable(clientType).Values;
        }

        private static readonly IReadOnlyList<string> s_verbs = ["login"];
        public IReadOnlyList<string> GetVerbs() => s_verbs;
    }
}
