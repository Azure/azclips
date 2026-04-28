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
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LoginCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.LoginCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TenantId"]       = new("TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UseDeviceCode"]  = new("UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["OutProfile"]     = new("OutProfile", typeof(string), "Save login result as a named profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_PS = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.LogoutCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Profile"] = new("Profile", typeof(string), "Name of the profile to log out.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getContext_PS = new(
            Name: "GetContextCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.GetContextCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.GetContextCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_login_CLI = new(
            Name: "LoginCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LoginCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.LoginCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["tenant-id"]       = new("TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["use-device-code"] = new("UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["out-profile"]     = new("OutProfile", typeof(string), "Save login result as a named profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_CLI = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.LogoutCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["profile"] = new("Profile", typeof(string), "Name of the profile to log out.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getContext_CLI = new(
            Name: "GetContextCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.GetContextCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.GetContextCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Profile Command Metadata (PS) ──
        private static readonly CommandMetadata s_listProfiles_PS = new(
            Name: "ListProfilesCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ListProfilesCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ListProfilesCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_showProfile_PS = new(
            Name: "ShowProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ShowProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ShowProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", typeof(string), "Name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_switchProfile_PS = new(
            Name: "SwitchProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.SwitchProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.SwitchProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", typeof(string), "Name of the profile to switch to.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_modifyProfile_PS = new(
            Name: "ModifyProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ModifyProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ModifyProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"]           = new("Name", typeof(string), "Name of the profile to modify.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "New subscription ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TenantId"]       = new("TenantId", typeof(string), "New tenant ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Set"]            = new("Set", typeof(string), "Set a default value (key=value).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RemoveDefault"]  = new("RemoveDefault", typeof(string), "Remove a default by key.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_removeProfile_PS = new(
            Name: "RemoveProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.RemoveProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.RemoveProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", typeof(string), "Name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Yes"]  = new("Yes", typeof(bool), "Skip confirmation prompt.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Profile Command Metadata (CLI) ──
        private static readonly CommandMetadata s_listProfiles_CLI = new(
            Name: "ListProfilesCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ListProfilesCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ListProfilesCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_showProfile_CLI = new(
            Name: "ShowProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ShowProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ShowProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Name of the profile to show.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_switchProfile_CLI = new(
            Name: "SwitchProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.SwitchProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.SwitchProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Name of the profile to switch to.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_modifyProfile_CLI = new(
            Name: "ModifyProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ModifyProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ModifyProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"]            = new("Name", typeof(string), "Name of the profile to modify.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subscription-id"] = new("SubscriptionId", typeof(string), "New subscription ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tenant-id"]       = new("TenantId", typeof(string), "New tenant ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["set"]             = new("Set", typeof(string), "Set a default value (key=value).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["remove-default"]  = new("RemoveDefault", typeof(string), "Remove a default by key.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_removeProfile_CLI = new(
            Name: "RemoveProfileCommand", Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.RemoveProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.RemoveProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Name of the profile to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["yes"]  = new("Yes", typeof(bool), "Skip confirmation prompt.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Connect-CirrusAccount"]    = s_login_PS,
            ["Disconnect-CirrusAccount"] = s_logout_PS,
            ["Get-CirrusAccount"]        = s_getContext_PS,
            ["Get-CirrusProfile"]        = s_listProfiles_PS,
            ["ListProfilesCommand"]      = s_listProfiles_PS,
            ["ShowProfileCommand"]       = s_showProfile_PS,
            ["Switch-CirrusProfile"]     = s_switchProfile_PS,
            ["Set-CirrusProfile"]        = s_modifyProfile_PS,
            ["Remove-CirrusProfile"]     = s_removeProfile_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            // login is a special command that the command group and command name are the same
            [""]       = s_login_CLI,
            ["logout"] = s_logout_CLI,
            ["show"]   = s_getContext_CLI,
        };
        // Profile commands use a separate table keyed by subcommand under "profile" group
        private static readonly Dictionary<string, CommandMetadata> s_profileCliTable = new()
        {
            ["list"]   = s_listProfiles_CLI,
            ["show"]   = s_showProfile_CLI,
            ["switch"] = s_switchProfile_CLI,
            ["modify"] = s_modifyProfile_CLI,
            ["remove"] = s_removeProfile_CLI,
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
            if (table.TryGetValue(name, out var metadata))
                return metadata;
            // Also check profile CLI subcommands
            if (clientType == ClientType.CLI && s_profileCliTable.TryGetValue(name, out metadata))
                return metadata;
            throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Values,
                ClientType.CLI => s_cliTable.Values.Concat(s_profileCliTable.Values),
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
    }
}