// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Graph
{
    [ExtensionVerb("msgraph")]
    public class ExtensionIndex : IExtensionIndex
    {
        // Descriptions are reused across parameter sets; centralize to avoid drift.

        // ── Shared ──
        private const string SelectDescription = "Properties to return. Combine with AppendSelected to include the default properties as well.";
        private const string AppendSelectedDescription = "When set with Select, include the default property set alongside the values in Select rather than replacing them.";
        private const string ConsistencyLevelDescription = "Set to 'eventual' to enable advanced query features such as $search, $count, or $orderby on non-indexed properties.";

        // ── User ──
        private const string UserIdDescription = "The object ID or user principal name (UPN) of the user.";
        private const string UserDisplayNameDescription = "List users with the specified display name.";
        private const string UserMailDescription = "List users with the specified mail address.";
        private const string UserStartsWithDescription = "List users whose display name starts with the specified prefix.";
        private const string UserPrincipalNameDescription = "List users with the specified user principal name.";
        private const string UserFilterDescription = "OData $filter expression, for example \"accountEnabled eq true\".";
        private const string UserOrderbyDescription = "OData $orderby values, for example 'displayName' or 'mail desc'.";
        private const string UserSearchDescription = "OData $search expression, for example \"displayName:ada\". Requires ConsistencyLevel eventual.";
        private const string UserExpandDescription = "Related properties to include in the response, for example memberOf.";

        // ── Group ──
        private const string GroupDescription = "The group's object ID or display name (prefix works if there is a unique match).";
        private const string GroupObjectIdDescription = "The object ID of the group.";
        private const string GroupDisplayNameDescription = "The exact display name of the target group.";
        private const string GroupDisplayNameFilterDescription = "List groups with the specified display name.";
        private const string GroupStartsWithDescription = "List groups whose display name starts with the specified prefix.";
        private const string GroupFilterDescription = "OData $filter expression, for example \"securityEnabled eq true\".";
        private const string GroupOrderbyDescription = "OData $orderby values, for example 'displayName' or 'createdDateTime desc'.";
        private const string GroupSearchDescription = "OData $search expression, for example \"displayName:contoso\". Requires ConsistencyLevel eventual.";
        private const string GroupExpandDescription = "Related properties to include in the response, for example members.";

        // ── Group member ──
        private const string MemberIdDescription = "The object ID of the member to test.";
        private const string GroupMemberFilterDescription = "OData $filter expression.";
        private const string GroupMemberOrderbyDescription = "OData $orderby values.";
        private const string GroupMemberSearchDescription = "OData $search expression. Requires ConsistencyLevel eventual.";
        private const string GroupMemberSelectDescription = "Properties to return.";
        private const string GroupMemberExpandDescription = "Related properties to include in the response.";

        // ── PS User Command Metadata ──
        private static readonly CommandMetadata s_getUser_PS = new(
            Name: "GetUserProcessor",
            InvocationName: "Get-AzMsGraphUser_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Id"]             = new("Id", "Id", typeof(string), UserIdDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), UserExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), UserExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listUsers_PS = new(
            Name: "ListUserProcessor",
            InvocationName: "Get-AzMsGraphUser_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Filter"]           = new("Filter", "Filter", typeof(string), UserFilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Select"]           = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]           = new("Expand", "Expand", typeof(string[]), UserExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Orderby"]          = new("Orderby", "Orderby", typeof(string[]), UserOrderbyDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Search"]           = new("Search", "Search", typeof(string), UserSearchDescription, IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["Mail"]           = new("Mail", "Mail", typeof(string), UserMailDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), UserExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["DisplayName"]    = new("DisplayName", "DisplayName", typeof(string), UserDisplayNameDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), UserExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["StartsWith"]     = new("DisplayNamePrefix", "StartsWith", typeof(string), UserStartsWithDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), UserExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── PS Group Command Metadata ──
        private static readonly CommandMetadata s_getGroup_PS = new(
            Name: "GetGroupProcessor",
            InvocationName: "Get-AzMsGraphGroup_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ObjectId"]       = new("Group", "ObjectId", typeof(string), GroupObjectIdDescription, IsRequired: true, Aliases: ["Id"], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), GroupExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listGroups_PS = new(
            Name: "ListGroupProcessor",
            InvocationName: "Get-AzMsGraphGroup_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Filter"]           = new("Filter", "Filter", typeof(string), GroupFilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Select"]           = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]           = new("Expand", "Expand", typeof(string[]), GroupExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Orderby"]          = new("Orderby", "Orderby", typeof(string[]), GroupOrderbyDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Search"]           = new("Search", "Search", typeof(string), GroupSearchDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ConsistencyLevel"] = new("ConsistencyLevel", "ConsistencyLevel", typeof(string), ConsistencyLevelDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"]   = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listGroupsByDisplayName_PS = new(
            Name: "ListGroupProcessor",
            InvocationName: "Get-AzMsGraphGroup_DisplayName",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["DisplayName"]    = new("DisplayName", "DisplayName", typeof(string), GroupDisplayNameFilterDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Select"]         = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]         = new("Expand", "Expand", typeof(string[]), GroupExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"] = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listGroupsByStartsWith_PS = new(
            Name: "ListGroupProcessor",
            InvocationName: "Get-AzMsGraphGroup_DisplayNameStartsWith",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["DisplayNameStartsWith"] = new("DisplayNamePrefix", "DisplayNameStartsWith", typeof(string), GroupStartsWithDescription, IsRequired: true, Aliases: ["SearchString"], AllowedValues: []),
                ["Select"]                = new("Select", "Select", typeof(string[]), SelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]                = new("Expand", "Expand", typeof(string[]), GroupExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["AppendSelected"]        = new("AppendSelected", "AppendSelected", typeof(bool), AppendSelectedDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── PS Group Member Command Metadata ──
        private static readonly CommandMetadata s_listGroupMembersByObjectId_PS = new(
            Name: "ListGroupMemberProcessor",
            InvocationName: "Get-AzMsGraphGroupMember_ObjectId",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupMemberProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupMemberProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["GroupObjectId"]    = new("Group", "GroupObjectId", typeof(string), GroupObjectIdDescription, IsRequired: true, Aliases: ["Id", "ObjectId"], AllowedValues: []),
                ["Filter"]           = new("Filter", "Filter", typeof(string), GroupMemberFilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Select"]           = new("Select", "Select", typeof(string[]), GroupMemberSelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]           = new("Expand", "Expand", typeof(string[]), GroupMemberExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Orderby"]          = new("Orderby", "Orderby", typeof(string[]), GroupMemberOrderbyDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Search"]           = new("Search", "Search", typeof(string), GroupMemberSearchDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ConsistencyLevel"] = new("ConsistencyLevel", "ConsistencyLevel", typeof(string), ConsistencyLevelDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listGroupMembersByDisplayName_PS = new(
            Name: "ListGroupMemberProcessor",
            InvocationName: "Get-AzMsGraphGroupMember_DisplayName",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupMemberProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupMemberProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["GroupDisplayName"] = new("GroupDisplayName", "GroupDisplayName", typeof(string), GroupDisplayNameDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["Filter"]           = new("Filter", "Filter", typeof(string), GroupMemberFilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Select"]           = new("Select", "Select", typeof(string[]), GroupMemberSelectDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Expand"]           = new("Expand", "Expand", typeof(string[]), GroupMemberExpandDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Orderby"]          = new("Orderby", "Orderby", typeof(string[]), GroupMemberOrderbyDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["Search"]           = new("Search", "Search", typeof(string), GroupMemberSearchDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["ConsistencyLevel"] = new("ConsistencyLevel", "ConsistencyLevel", typeof(string), ConsistencyLevelDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_checkGroupMember_PS = new(
            Name: "CheckGroupMemberProcessor",
            InvocationName: "Test-AzMsGraphGroupMembership",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.CheckGroupMemberProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.CheckGroupMemberProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Group"]    = new("Group", "Group", typeof(string), GroupDescription, IsRequired: true, Aliases: [], AllowedValues: []),
                ["MemberId"] = new("MemberId", "MemberId", typeof(string), MemberIdDescription, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── CLI User Command Metadata ──
        private static readonly CommandMetadata s_getUser_CLI = new(
            Name: "GetUserProcessor",
            InvocationName: "msgraph user show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["id"] = new("Id", "id", typeof(string), UserIdDescription, IsRequired: true, Aliases: [], AllowedValues: []),
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
                ["display-name"] = new("DisplayNamePrefix", "display-name", typeof(string), UserStartsWithDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["filter"]       = new("Filter", "filter", typeof(string), UserFilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["upn"]          = new("UserPrincipalName", "upn", typeof(string), UserPrincipalNameDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── CLI Group Command Metadata ──
        private static readonly CommandMetadata s_getGroup_CLI = new(
            Name: "GetGroupProcessor",
            InvocationName: "msgraph group show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["group"] = new("Group", "group", typeof(string), GroupDescription, IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "group" }
        );
        private static readonly CommandMetadata s_listGroups_CLI = new(
            Name: "ListGroupProcessor",
            InvocationName: "msgraph group list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["display-name"] = new("DisplayNamePrefix", "display-name", typeof(string), GroupStartsWithDescription, IsRequired: false, Aliases: [], AllowedValues: []),
                ["filter"]       = new("Filter", "filter", typeof(string), GroupFilterDescription, IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── CLI Group Member Command Metadata ──
        private static readonly CommandMetadata s_listGroupMembers_CLI = new(
            Name: "ListGroupMemberProcessor",
            InvocationName: "msgraph group member list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.ListGroupMemberProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.ListGroupMemberProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["group"] = new("Group", "group", typeof(string), GroupDescription, IsRequired: true, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "group" }
        );
        private static readonly CommandMetadata s_checkGroupMember_CLI = new(
            Name: "CheckGroupMemberProcessor",
            InvocationName: "msgraph group member check",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.CheckGroupMemberProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.CheckGroupMemberProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["group"]     = new("Group", "group", typeof(string), GroupDescription, IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["member-id"] = new("MemberId", "member-id", typeof(string), MemberIdDescription, IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["g"] = "group" }
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            // User
            ["Get-AzMsGraphUser_Get"]          = s_getUser_PS,
            ["Get-AzMsGraphUser_SignedIn"]     = s_getSignedInUser_PS,
            ["Get-AzMsGraphUser_List"]         = s_listUsers_PS,
            ["Get-AzMsGraphUser_Mail"]         = s_listUsersByMail_PS,
            ["Get-AzMsGraphUser_DisplayName"]  = s_listUsersByDisplayName_PS,
            ["Get-AzMsGraphUser_StartsWith"]   = s_listUsersByStartsWith_PS,
            // Group
            ["Get-AzMsGraphGroup_Get"]                    = s_getGroup_PS,
            ["Get-AzMsGraphGroup_List"]                   = s_listGroups_PS,
            ["Get-AzMsGraphGroup_DisplayName"]            = s_listGroupsByDisplayName_PS,
            ["Get-AzMsGraphGroup_DisplayNameStartsWith"]  = s_listGroupsByStartsWith_PS,
            // Group member
            ["Get-AzMsGraphGroupMember_ObjectId"]         = s_listGroupMembersByObjectId_PS,
            ["Get-AzMsGraphGroupMember_DisplayName"]      = s_listGroupMembersByDisplayName_PS,
            ["Test-AzMsGraphGroupMembership"]             = s_checkGroupMember_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            // User
            ["msgraph user show"]          = s_getUser_CLI,
            ["msgraph user list"]          = s_listUsers_CLI,
            // Group
            ["msgraph group show"]         = s_getGroup_CLI,
            ["msgraph group list"]         = s_listGroups_CLI,
            // Group member
            ["msgraph group member list"]  = s_listGroupMembers_CLI,
            ["msgraph group member check"] = s_checkGroupMember_CLI,
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
