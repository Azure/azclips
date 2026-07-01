// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource;

namespace Microsoft.Azure.Azclips.Extensions.Agent
{
    [ExtensionVerb("agent")]
    public class ExtensionIndex : IExtensionIndex
    {
        // Agent tool commands wrap internal agent tools so external programs
        // (Copilot CLI / Claude Code Skills) can invoke them directly instead
        // of spawning a full AI conversation loop.

        // ── CLI Command Metadata ──

        private static readonly CommandMetadata s_env_CLI = new(
            Name: "EnvProcessor",
            InvocationName: "agent tool env",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.EnvProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(EnvProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_help_CLI = new(
            Name: "HelpProcessor",
            InvocationName: "agent tool help",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.HelpProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(HelpProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "Command name to look up (e.g. 'vm create'). Omit to list all known commands.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_resourceFind_CLI = new(
            Name: "ResourceFindProcessor",
            InvocationName: "agent tool resource find",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource.ResourceFindProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(ResourceFindProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["query"] = new("Query", "query", typeof(string), "Substring to match against resource names.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["kind"]  = new("Kind", "kind", typeof(string), "Resource kind filter (e.g. 'resources', 'resource-groups'). Defaults to all kinds.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_resourceSku_CLI = new(
            Name: "ResourceSkuProcessor",
            InvocationName: "agent tool resource sku",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource.ResourceSkuProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(ResourceSkuProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["vm-size"]         = new("VmSize", "vm-size", typeof(string), "VM SKU to check, e.g. 'Standard_D2s_v5'.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Optional subscription id. Defaults to the current auth-context subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Dispatch Tables ──
        // Full-path keys keyed on the space-joined invocation path. The root
        // verb ("agent") isn't a runnable leaf — dispatcher treats it as a group.

        private static readonly Dictionary<string, CommandMetadata> s_psTable = new();

        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["agent tool env"]            = s_env_CLI,
            ["agent tool help"]           = s_help_CLI,
            ["agent tool resource find"]  = s_resourceFind_CLI,
            ["agent tool resource sku"]   = s_resourceSku_CLI,
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

        private static readonly IReadOnlyList<string> s_verbs = ["agent"];
        public IReadOnlyList<string> GetVerbs() => s_verbs;
    }
}
