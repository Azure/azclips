// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Graph
{
    [ExtensionVerb("msgraph")]
    public class ExtensionIndex : IExtensionIndex
    {
        // Descriptions are reused across parameter sets; centralize to avoid drift.
        private const string FilterDescription = "OData $filter expression applied by Microsoft Graph. Passed through verbatim; Graph validates the syntax.";
        private const string SelectDescription = "Properties to project via OData $select. Combine with AppendSelected to widen rather than replace the default property set.";
        private const string ExpandDescription = "Related navigation properties to expand via OData $expand (for example, memberOf).";
        private const string OrderbyDescription = "Property paths to sort by via OData $orderby (for example, 'displayName', 'mail desc').";
        private const string SearchDescription = "Free-text search expression mapped to OData $search (for example, 'displayName:ada').";
        private const string ConsistencyLevelDescription = "Value sent as the ConsistencyLevel HTTP header. Required (typically 'eventual') when using advanced queries such as $search, $count, or $orderby on non-indexed properties.";
        private const string AppendSelectedDescription = "When set, prepends the default Microsoft Graph user property set to the values supplied via Select instead of replacing them.";
        private const string IdDescription = "The object ID or user principal name (UPN) of the user.";
        private const string DisplayNameDescription = "Exact display name to match. Translated to the OData filter displayName eq 'value'.";
        private const string MailDescription = "Exact mail address to match. Translated to the OData filter mail eq 'value'.";
        private const string StartsWithDescription = "Prefix to match against the display name. Translated to the OData filter startswith(displayName,'value').";
        private const string UserPrincipalNameDescription = "Exact user principal name to match. Translated to the OData filter userPrincipalName eq 'value'.";

        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_listUsers_PS = new(
            Name: "ListUserProcessor",
            InvocationName: "Get-AzMsGraphUser_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Filter"]           = new("Filter", "Filter", typeof(string), FilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Select"]           = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]           = new("Expand", "Expand", typeof(string[]), ExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Orderby"]          = new("Orderby", "Orderby", typeof(string[]), OrderbyDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Search"]           = new("Search", "Search", typeof(string), SearchDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ConsistencyLevel"] = new("ConsistencyLevel", "ConsistencyLevel", typeof(string), ConsistencyLevelDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"]   = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listUsersByMail_PS = new(
            Name: "ListUserProcessor",
            InvocationName: "Get-AzMsGraphUser_Mail",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Mail"]           = new("Mail", "Mail", typeof(string), MailDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), ExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listUsersByDisplayName_PS = new(
            Name: "ListUserProcessor",
            InvocationName: "Get-AzMsGraphUser_DisplayName",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["DisplayName"]    = new("DisplayName", "DisplayName", typeof(string), DisplayNameDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), ExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listUsersByStartsWith_PS = new(
            Name: "ListUserProcessor",
            InvocationName: "Get-AzMsGraphUser_StartsWith",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["StartsWith"]     = new("StartsWith", "StartsWith", typeof(string), StartsWithDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), ExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getUser_PS = new(
            Name: "GetUserProcessor",
            InvocationName: "Get-AzMsGraphUser_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Id"]             = new("Id", "Id", typeof(string), IdDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), ExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getSignedInUser_PS = new(
            Name: "GetSignedInUserProcessor",
            InvocationName: "Get-AzMsGraphUser_SignedIn",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetSignedInUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetSignedInUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), ExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_getUser_CLI = new(
            Name: "GetUserProcessor",
            InvocationName: "msgraph user show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["id"] = new("Id", "id", typeof(string), IdDescription, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listUsers_CLI = new(
            Name: "ListUserProcessor",
            InvocationName: "msgraph user list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["display-name"] = new("DisplayName", "display-name", typeof(string), DisplayNameDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["filter"]       = new("Filter", "filter", typeof(string), FilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["upn"]          = new("UserPrincipalName", "upn", typeof(string), UserPrincipalNameDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Get-AzMsGraphUser_List"]        = s_listUsers_PS,
            ["Get-AzMsGraphUser_Mail"]        = s_listUsersByMail_PS,
            ["Get-AzMsGraphUser_DisplayName"] = s_listUsersByDisplayName_PS,
            ["Get-AzMsGraphUser_StartsWith"]  = s_listUsersByStartsWith_PS,
            ["Get-AzMsGraphUser_Get"]         = s_getUser_PS,
            ["Get-AzMsGraphUser_SignedIn"]    = s_getSignedInUser_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["msgraph user show"] = s_getUser_CLI,
            ["msgraph user list"] = s_listUsers_CLI,
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

        private static readonly IReadOnlyList<string> s_verbs = ["msgraph"];
        public IReadOnlyList<string> GetVerbs() => s_verbs;
    }
}
