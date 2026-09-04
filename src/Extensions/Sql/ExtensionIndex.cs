// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Sql
{
    [ExtensionVerb("sql")]
    public class ExtensionIndex : IExtensionIndex
    {
        // PS Command Metadata
        private static readonly CommandMetadata s_newSqlServer_PS = new(
            Name: "NewSqlServerProcessor",
            InvocationName: "New-AzSqlServer",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Sql.Processors.NewSqlServerProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Sql",
            ProcessorType: typeof(Processors.NewSqlServerProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]             = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]          = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]                       = new("Name", "Name", typeof(string), "The name of the SQL server.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]                   = new("Location", "Location", typeof(string), "Location for the SQL server.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdministratorLogin"]         = new("AdministratorLogin", "AdministratorLogin", typeof(string), "The administrator login name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AdministratorLoginPassword"] = new("AdministratorLoginPassword", "AdministratorLoginPassword", typeof(string), "The administrator login password.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ServerVersion"]              = new("ServerVersion", "ServerVersion", typeof(string), "The version of the SQL server (e.g. 12.0).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["MinimalTlsVersion"]          = new("MinimalTlsVersion", "MinimalTlsVersion", typeof(string), "Minimal TLS version enforced by the server.", IsRequired: false, Aliases: [], AllowedValues: ["None", "1.0", "1.1", "1.2", "1.3"]),
                ["PublicNetworkAccess"]        = new("PublicNetworkAccess", "PublicNetworkAccess", typeof(string), "Whether public network access is allowed.", IsRequired: false, Aliases: [], AllowedValues: ["Enabled", "Disabled", "SecuredByPerimeter"]),
                ["Tags"]                       = new("Tags", "Tags", typeof(string[]), "Resource tags as key=value pairs.", IsRequired: false, Aliases: ["Tag"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // CLI Command Metadata
        private static readonly CommandMetadata s_newSqlServer_CLI = new(
            Name: "NewSqlServerProcessor",
            InvocationName: "sql server create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Sql.Processors.NewSqlServerProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Sql",
            ProcessorType: typeof(Processors.NewSqlServerProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]        = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]         = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["name"]                   = new("Name", "name", typeof(string), "The name of the SQL server.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["location"]               = new("Location", "location", typeof(string), "Location for the SQL server.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["admin-user"]             = new("AdministratorLogin", "admin-user", typeof(string), "The administrator login name.", IsRequired: true, Aliases: ["u"], AllowedValues: []),
                ["admin-password"]         = new("AdministratorLoginPassword", "admin-password", typeof(string), "The administrator login password.", IsRequired: true, Aliases: ["p"], AllowedValues: []),
                ["server-version"]         = new("ServerVersion", "server-version", typeof(string), "The version of the SQL server (e.g. 12.0).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["minimal-tls-version"]    = new("MinimalTlsVersion", "minimal-tls-version", typeof(string), "Minimal TLS version enforced by the server.", IsRequired: false, Aliases: [], AllowedValues: ["None", "1.0", "1.1", "1.2", "1.3"]),
                ["public-network-access"]  = new("PublicNetworkAccess", "public-network-access", typeof(string), "Whether public network access is allowed.", IsRequired: false, Aliases: [], AllowedValues: ["Enabled", "Disabled", "SecuredByPerimeter"]),
                ["tags"]                   = new("Tags", "tags", typeof(string[]), "Resource tags as space-separated key[=value] pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
                ["l"] = "location",
                ["u"] = "admin-user",
                ["p"] = "admin-password",
            }
        );

        // Dispatch Tables
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzSqlServer"] = s_newSqlServer_PS,
        };

        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["sql server create"] = s_newSqlServer_CLI,
        };

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

        // Lookup
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
