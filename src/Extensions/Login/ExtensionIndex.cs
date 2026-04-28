// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Login
{
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_login_PS = new(
            Name: "LoginCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TenantId"]       = new("TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UseDeviceCode"]  = new("UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["OutProfile"]     = new("OutProfile", typeof(string), "Name of the profile to create or overwrite.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_PS = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["TargetProfile"] = new("TargetProfile", typeof(string), "Profile to logout from.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getContext_PS = new(
            Name: "GetContextCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.GetContextProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.GetContextProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileShow_PS = new(
            Name: "ProfileShowCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileShowProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", typeof(string), "Name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileList_PS = new(
            Name: "ProfileListCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileListProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileSwitch_PS = new(
            Name: "ProfileSwitchCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileSwitchProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileSwitchProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", typeof(string), "Name of the profile to switch to.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileModify_PS = new(
            Name: "ProfileModifyCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileModifyProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileModifyProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"]          = new("Name", typeof(string), "Name of the profile to modify.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Subscription"]  = new("Subscription", typeof(string), "New subscription ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TenantId"]      = new("TenantId", typeof(string), "New tenant ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Default"]       = new("Default", typeof(string), "Set a default (key=value).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RemoveDefault"] = new("RemoveDefault", typeof(string), "Remove a default by key.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileRemove_PS = new(
            Name: "ProfileRemoveCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileRemoveProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileRemoveProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", typeof(string), "Name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_login_CLI = new(
            Name: "LoginCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tenant-id"]       = new("TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["use-device-code"] = new("UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["out-profile"]     = new("OutProfile", typeof(string), "Name of the profile to create or overwrite.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_CLI = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["target-profile"] = new("TargetProfile", typeof(string), "Profile to logout from.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getContext_CLI = new(
            Name: "GetContextCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.GetContextProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.GetContextProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileShow_CLI = new(
            Name: "ProfileShowCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileShowProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileList_CLI = new(
            Name: "ProfileListCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileListProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileSwitch_CLI = new(
            Name: "ProfileSwitchCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileSwitchProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileSwitchProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Name of the profile to switch to.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileModify_CLI = new(
            Name: "ProfileModifyCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileModifyProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileModifyProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"]           = new("Name", typeof(string), "Name of the profile to modify.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subscription"]   = new("Subscription", typeof(string), "New subscription ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tenant-id"]      = new("TenantId", typeof(string), "New tenant ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["default"]        = new("Default", typeof(string), "Set a default (key=value).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["remove-default"] = new("RemoveDefault", typeof(string), "Remove a default by key.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_profileRemove_CLI = new(
            Name: "ProfileRemoveCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.ProfileRemoveProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.ProfileRemoveProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Connect-CirrusAccount"]    = s_login_PS,
            ["Disconnect-CirrusAccount"] = s_logout_PS,
            ["Get-CirrusAccount"]        = s_getContext_PS,
            ["Get-CirrusProfile"]        = s_profileShow_PS,
            ["Switch-CirrusProfile"]     = s_profileSwitch_PS,
            ["Set-CirrusProfile"]        = s_profileModify_PS,
            ["Remove-CirrusProfile"]     = s_profileRemove_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            // login is a special command that the command group and command name are the same
            [""]               = s_login_CLI,
            ["logout"]         = s_logout_CLI,
            ["show"]           = s_profileShow_CLI,
            ["profile list"]   = s_profileList_CLI,
            ["profile show"]   = s_profileShow_CLI,
            ["profile switch"] = s_profileSwitch_CLI,
            ["profile modify"] = s_profileModify_CLI,
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