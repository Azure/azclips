// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Web
{
    [ExtensionVerb("webapp")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_deleteWebappOrchestrator_PS = new(
            Name: "DeleteWebappProcessor_Orchestrator",
            InvocationName: "Remove-Webapp",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Web.Processors.DeleteWebappProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Web",
            ProcessorType: typeof(Processors.DeleteWebappProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the web app.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["DeleteMetrics"] = new("DeleteMetrics", "DeleteMetrics", typeof(bool), "If true, web app metrics are also deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DeleteEmptyServerFarm"] = new("DeleteEmptyServerFarm", "DeleteEmptyServerFarm", typeof(bool), "Specify false if you want to keep empty App Service plan. By default, empty App Service plan is deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Slot"] = new("Slot", "Slot", typeof(string), "Name of the deployment slot to delete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_listWebappOrchestrator_PS = new(
            Name: "ListWebappProcessor_Orchestrator",
            InvocationName: "List-Webapp",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Web.Processors.ListWebappProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Web",
            ProcessorType: typeof(Processors.ListWebappProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_deleteWebappOrchestrator_CLI = new(
            Name: "DeleteWebappProcessor_Orchestrator",
            InvocationName: "webapp delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Web.Processors.DeleteWebappProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Web",
            ProcessorType: typeof(Processors.DeleteWebappProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the web app.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["delete-metrics"] = new("DeleteMetrics", "delete-metrics", typeof(bool), "If true, web app metrics are also deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["delete-empty-server-farm"] = new("DeleteEmptyServerFarm", "delete-empty-server-farm", typeof(bool), "Specify false if you want to keep empty App Service plan. By default, empty App Service plan is deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["slot"] = new("Slot", "slot", typeof(string), "Name of the deployment slot to delete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_listWebappOrchestrator_CLI = new(
            Name: "ListWebappProcessor_Orchestrator",
            InvocationName: "webapp list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Web.Processors.ListWebappProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Web",
            ProcessorType: typeof(Processors.ListWebappProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Remove-Webapp"]                = s_deleteWebappOrchestrator_PS,
            ["List-Webapp"]                = s_listWebappOrchestrator_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["webapp delete"]   = s_deleteWebappOrchestrator_CLI,
            ["webapp list"]   = s_listWebappOrchestrator_CLI,
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
