// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Graph
{
    [ExtensionVerb("msgraph")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_getUser_PS = new(
            Name: "GetUserProcessor",
            InvocationName: "Get-AzMsGraphUser",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Graph.Processors.GetUserProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Graph",
            ProcessorType: typeof(Processors.GetUserProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Id"] = new("Id", "Id", typeof(string), "The object ID or user principal name (UPN) of the user.", IsRequired: true, Aliases: [], AllowedValues: []),
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
                ["id"] = new("Id", "id", typeof(string), "The object ID or user principal name (UPN) of the user.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Get-AzMsGraphUser"] = s_getUser_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches an [ExtensionVerb]
        // declaration on this class; the dispatcher does longest-prefix match on the (verb, path)
        // tuple via Core.Extensions.LeafIndex. Graph contributes the "msgraph" verb.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["msgraph user show"] = s_getUser_CLI,
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
