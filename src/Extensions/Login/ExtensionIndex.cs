// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Login
{
    [ExtensionVerb("login")]
    [ExtensionVerb("logout")]
    [ExtensionVerb("profile")]
    public class ExtensionIndex : IExtensionIndex
    {
        private const string PSCommandPrefix = "Az";

#region PS Command Metadata
        private static readonly CommandMetadata s_login_PS = new(
            Name: nameof(Processors.LoginProcessor),
            InvocationName: $"Connect-{PSCommandPrefix}Account",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TenantId"]       = new("TenantId", "TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UseDeviceCode"]  = new("UseDeviceCode", "UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["OutProfile"]     = new("OutProfile", "OutProfile", typeof(string), "Create or overwrite a named profile with the login result (mutually exclusive with -Profile).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"]        = new("Profile", "Profile", typeof(string), "Re-authenticate the named existing profile (mutually exclusive with -OutProfile).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_PS = new(
            Name: nameof(Processors.LogoutProcessor),
            InvocationName: $"Disconnect-{PSCommandPrefix}Account",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileList_PS = new(
            Name: nameof(Processors.ProfileListProcessor),
            InvocationName: $"Get-{PSCommandPrefix}Profile_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileListProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileShow_PS = new(
            Name: nameof(Processors.ProfileShowProcessor),
            InvocationName: $"Get-{PSCommandPrefix}Profile_Show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileShowProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Active"] = new("Active", "Active", typeof(bool), "Show the active profile. Cannot be used together with Name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileSwitch_PS = new(
            Name: nameof(Processors.ProfileSwitchProcessor),
            InvocationName: $"Set-{PSCommandPrefix}Profile",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileSwitchProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileSwitchProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The name of the profile to activate.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileRemove_PS = new(
            Name: nameof(Processors.ProfileRemoveProcessor),
            InvocationName: $"Remove-{PSCommandPrefix}Profile",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileRemoveProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileRemoveProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
#endregion

#region CLI Command Metadata
        private static readonly CommandMetadata s_login_CLI = new(
            Name: nameof(Processors.LoginProcessor),
            InvocationName: "login",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tenant-id"]       = new("TenantId", "tenant-id", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["use-device-code"] = new("UseDeviceCode", "use-device-code", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["out-profile"]     = new("OutProfile", "out-profile", typeof(string), "Create or overwrite a named profile with the login result (mutually exclusive with --profile).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["profile"]         = new("Profile", "profile", typeof(string), "Re-authenticate the named existing profile (mutually exclusive with --out-profile).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_CLI = new(
            Name: nameof(Processors.LogoutProcessor),
            InvocationName: "logout",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileList_CLI = new(
            Name: nameof(Processors.ProfileListProcessor),
            InvocationName: "profile list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileListProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileShow_CLI = new(
            Name: nameof(Processors.ProfileShowProcessor),
            InvocationName: "profile show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileShowProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                // The profile name is accepted positionally (`profile show <name>`) or via --name (aliased to the positional slot).
                ["name"] = new("Name", "name", typeof(string), "The name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["active"] = new("Active", "active", typeof(bool), "Show the active profile. Cannot be used together with the profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { [""] = "name" }
        );
        private static readonly CommandMetadata s_profileSwitch_CLI = new(
            Name: nameof(Processors.ProfileSwitchProcessor),
            InvocationName: "profile set",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileSwitchProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileSwitchProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the profile to activate.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { [""] = "name" }
        );
        private static readonly CommandMetadata s_profileRemove_CLI = new(
            Name: nameof(Processors.ProfileRemoveProcessor),
            InvocationName: "profile remove",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Login.Processors.ProfileRemoveProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileRemoveProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { [""] = "name" }
        );
#endregion

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            [$"Connect-{PSCommandPrefix}Account"]    = s_login_PS,
            [$"Disconnect-{PSCommandPrefix}Account"] = s_logout_PS,
            [$"Get-{PSCommandPrefix}Profile_List"]   = s_profileList_PS,
            [$"Get-{PSCommandPrefix}Profile_Show"]   = s_profileShow_PS,
            [$"Switch-{PSCommandPrefix}Profile"]     = s_profileSwitch_PS,
            [$"Remove-{PSCommandPrefix}Profile"]     = s_profileRemove_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["login"]          = s_login_CLI,
            ["logout"]   = s_logout_CLI,
            ["profile list"]   = s_profileList_CLI,
            ["profile show"]   = s_profileShow_CLI,
            ["profile switch"] = s_profileSwitch_CLI,
            ["profile remove"] = s_profileRemove_CLI,
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

        private static readonly IReadOnlyList<string> s_verbs = ["login", "logout", "profile"];
        public IReadOnlyList<string> GetVerbs() => s_verbs;
    }
}
