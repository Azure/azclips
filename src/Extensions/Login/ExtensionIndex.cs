// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Login
{
    public class ExtensionIndex : IExtensionIndex, IExtensionHelpProvider
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
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TenantId"]       = new("TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UseDeviceCode"]  = new("UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_logout_PS = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
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
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_login_CLI = new(
            Name: "LoginCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LoginProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LoginProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["tenant-id"]       = new("TenantId", typeof(string), "Tenant ID to login to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["use-device-code"] = new("UseDeviceCode", typeof(bool), "Use device code flow for authentication.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(),
            Summary: "Authenticate to Azure and set the current account context.",
            Description: "Authenticate to Azure and persist account context for subsequent commands.",
            Examples:
            [
                new("Login with subscription id", "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000"),
                new("Login with device code",      "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code"),
            ]
        );
        private static readonly CommandMetadata s_logout_CLI = new(
            Name: "LogoutCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.LogoutProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.LogoutProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>(),
            Summary: "Sign out from the current Azure account context.",
            Description: "Sign out from the current Azure account context.",
            Examples: [new("Sign out", "azv2 login logout")]
        );
        private static readonly CommandMetadata s_getContext_CLI = new(
            Name: "GetContextCommand",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Processors.GetContextProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
            ProcessorType: typeof(Processors.GetContextProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>(),
            Summary: "Show the current Azure account context.",
            Description: "Show the current Azure account context.",
            Examples: [new("Show current account context", "azv2 login show")]
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
        };

        private static readonly RuntimeHelpGroup s_loginGroup = new(
            GroupPath: "login",
            Summary: "Authenticate to Azure.",
            Subgroups: [],
            Commands:
            [
                new RuntimeHelpCommandSummary(
                    Name: "login",
                    Summary: "Authenticate to Azure and set the current account context.",
                    Tags: []),
                new RuntimeHelpCommandSummary(
                    Name: "logout",
                    Summary: "Sign out from the current Azure account context.",
                    Tags: []),
                new RuntimeHelpCommandSummary(
                    Name: "show",
                    Summary: "Show the current Azure account context.",
                    Tags: [])
            ]);

        private static readonly RuntimeHelpCommand s_loginCommand = new(
            CommandPath: "login",
            Summary: "Authenticate to Azure and set the current account context.",
            Description: "Authenticate to Azure and persist account context for subsequent commands.",
            Parameters:
            [
                new RuntimeHelpParameter(
                    Name: "subscription-id",
                    Aliases: [],
                    Required: true,
                    Description: "Azure Subscription Id.",
                    Summary: "Azure Subscription Id.",
                    AllowedValues: [],
                    ParameterSetName: "(All)",
                    SupportsPipeline: false,
                    SupportsWildcards: false),
                new RuntimeHelpParameter(
                    Name: "tenant-id",
                    Aliases: [],
                    Required: false,
                    Description: "Tenant ID to login to.",
                    Summary: "Tenant ID to login to.",
                    AllowedValues: [],
                    ParameterSetName: "(All)",
                    SupportsPipeline: false,
                    SupportsWildcards: false),
                new RuntimeHelpParameter(
                    Name: "use-device-code",
                    Aliases: [],
                    Required: false,
                    Description: "Use device code flow for authentication.",
                    Summary: "Use device code flow for authentication.",
                    AllowedValues: [],
                    ParameterSetName: "(All)",
                    SupportsPipeline: false,
                    SupportsWildcards: false)
            ],
            Examples:
            [
                new RuntimeHelpExample(
                    Title: "Login with subscription id",
                    Command: "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000"),
                new RuntimeHelpExample(
                    Title: "Login with device code",
                    Command: "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code")
            ],
            Tags: []);

        private static readonly RuntimeHelpCommand s_logoutCommand = new(
            CommandPath: "login logout",
            Summary: "Sign out from the current Azure account context.",
            Description: "Sign out from the current Azure account context.",
            Parameters: [],
            Examples:
            [
                new RuntimeHelpExample(
                    Title: "Sign out",
                    Command: "azv2 login logout")
            ],
            Tags: []);

        private static readonly RuntimeHelpCommand s_showCommand = new(
            CommandPath: "login show",
            Summary: "Show the current Azure account context.",
            Description: "Show the current Azure account context.",
            Parameters: [],
            Examples:
            [
                new RuntimeHelpExample(
                    Title: "Show current account context",
                    Command: "azv2 login show")
            ],
            Tags: []);
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

        public string ExtensionName => "Login";

        public IReadOnlyList<RuntimeHelpGroup> GetRootGroups()
            => [s_loginGroup];

        public RuntimeHelpGroup? GetGroupHelp(IReadOnlyList<string> groupPath)
        {
            if (groupPath.Count == 1 && groupPath[0].Equals("login", StringComparison.OrdinalIgnoreCase))
            {
                return s_loginGroup;
            }

            return null;
        }

        public RuntimeHelpCommand? GetCommandHelp(IReadOnlyList<string> commandPath)
        {
            if (commandPath.Count == 1 && commandPath[0].Equals("login", StringComparison.OrdinalIgnoreCase))
            {
                return s_loginCommand;
            }

            if (commandPath.Count == 2 &&
                commandPath[0].Equals("login", StringComparison.OrdinalIgnoreCase) &&
                commandPath[1].Equals("logout", StringComparison.OrdinalIgnoreCase))
            {
                return s_logoutCommand;
            }

            if (commandPath.Count == 2 &&
                commandPath[0].Equals("login", StringComparison.OrdinalIgnoreCase) &&
                commandPath[1].Equals("show", StringComparison.OrdinalIgnoreCase))
            {
                return s_showCommand;
            }

            return null;
        }
    }
}