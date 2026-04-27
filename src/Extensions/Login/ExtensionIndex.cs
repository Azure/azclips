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
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_PS = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.LogoutCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
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
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_CLI = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.LogoutCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
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
        private static readonly CommandMetadata s_listProfiles_CLI = new(
            Name: "ListProfilesCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ListProfilesCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ListProfilesCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_showActiveProfile_CLI = new(
            Name: "ShowActiveProfileCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.ShowActiveProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.ShowActiveProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_setActiveProfile_CLI = new(
            Name: "SetActiveProfileCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.SetActiveProfileCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            CommandType: typeof(Commands.SetActiveProfileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", typeof(string), "Profile name to set active.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Connect-CirrusAccount"]    = s_login_PS,
            ["Disconnect-CirrusAccount"] = s_logout_PS,
            ["Get-CirrusAccount"]        = s_getContext_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            // login is a special command that the command group and command name are the same
            [""]       = s_login_CLI,
            ["logout"] = s_logout_CLI,
            ["show"]   = s_getContext_CLI,
            ["list"]   = s_listProfiles_CLI,
            ["active"] = s_showActiveProfile_CLI,
            ["set"]    = s_setActiveProfile_CLI,
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