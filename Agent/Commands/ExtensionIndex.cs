// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot;

namespace Microsoft.Azure.Azclips.Extensions.Agent
{
    [ExtensionVerb("agent")]
    [ExtensionVerb("troubleshoot")]
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
                ["name"] = new("Name", "name", typeof(string), "Name pattern to match against cached Azure resource names.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["kind"] = new("Kind", "kind", typeof(string), "Optional kind filter (tenant | subscription | resourceGroup | resource). Defaults to all kinds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription-id"] = new("Subscription", "subscription-id", typeof(string), "Optional subscription id or display name to narrow the search.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Optional resource group name to narrow the search (only meaningful for kind=resource).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["top"] = new("Top", "top", typeof(int), "Max candidates to return. Default 10.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_helpGenerate_CLI = new(
            Name: "HelpGenerateProcessor",
            InvocationName: "agent tool help generate",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.HelpGenerateProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(HelpGenerateProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
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

        private static readonly CommandMetadata s_resourceTree_CLI = new(
            Name: "ResourceTreeProcessor",
            InvocationName: "agent tool resource tree",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource.ResourceTreeProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(ResourceTreeProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"]      = new("Name", "name", typeof(string), "Name, full ARM id, subscription id/name, or tenant id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["kind"]      = new("Kind", "kind", typeof(string), "Kind of node to root the tree at: tenant | subscription | resourceGroup | resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["max-depth"] = new("MaxDepth", "max-depth", typeof(int), "Max depth from the root node. Defaults to 2.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription-id"] = new("Subscription", "subscription-id", typeof(string), "Optional subscription scope when kind=resourceGroup and the RG name repeats across subscriptions.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_resourceShow_CLI = new(
            Name: "ResourceShowProcessor",
            InvocationName: "agent tool resource show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource.ResourceShowProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(ResourceShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "Name or full ARM id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["kind"] = new("Kind", "kind", typeof(string), "Optional kind filter: tenant | subscription | resourceGroup | resource.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription-id"] = new("Subscription", "subscription-id", typeof(string), "Optional subscription id or display name to scope the lookup.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Optional resource group to scope the lookup (only meaningful for kind=resource).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["live"] = new("Live", "live", typeof(bool), "Fetch live ARM body (properties, provisioningState, endpoints). Adds ~200-500ms. Default false.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_resourceRefresh_CLI = new(
            Name: "ResourceRefreshProcessor",
            InvocationName: "agent tool resource refresh",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource.ResourceRefreshProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(ResourceRefreshProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Dispatch Tables ──
        // Full-path keys keyed on the space-joined invocation path. The root
        // verb ("agent") isn't a runnable leaf — dispatcher treats it as a group.

        private static readonly CommandMetadata s_troubleshootRecover_CLI = new(
            Name: "TroubleshootRecoverProcessor",
            InvocationName: "troubleshoot recover",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot.TroubleshootRecoverProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(TroubleshootRecoverProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["id"] = new("Id", "id", typeof(string), "Recover from this exact saved failure. Omit to select the latest saved failure in this terminal, then the global latest.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["style"] = new("Style", "style", typeof(string), "Override agent.recovery.style for this recovery only.", IsRequired: false, Aliases: [], AllowedValues: ["oneshot", "interactive"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_troubleshootList_CLI = new(
            Name: "TroubleshootListProcessor",
            InvocationName: "troubleshoot list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot.TroubleshootListProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(TroubleshootListProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_troubleshootShow_CLI = new(
            Name: "TroubleshootShowProcessor",
            InvocationName: "troubleshoot show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot.TroubleshootShowProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Agent",
            ProcessorType: typeof(TroubleshootShowProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["id"] = new("Id", "id", typeof(string), "Show this exact saved failure locally, including the complete command and error detail.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly Dictionary<string, CommandMetadata> s_psTable = new();

        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["agent tool env"]            = s_env_CLI,
            ["agent tool help"]           = s_help_CLI,
            ["agent tool help generate"]  = s_helpGenerate_CLI,
            ["agent tool resource find"]  = s_resourceFind_CLI,
            ["agent tool resource sku"]   = s_resourceSku_CLI,
            ["agent tool resource tree"]  = s_resourceTree_CLI,
            ["agent tool resource show"]  = s_resourceShow_CLI,
            ["agent tool resource refresh"]  = s_resourceRefresh_CLI,
            ["troubleshoot recover"]      = s_troubleshootRecover_CLI,
            ["troubleshoot list"]         = s_troubleshootList_CLI,
            ["troubleshoot show"]         = s_troubleshootShow_CLI,
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
