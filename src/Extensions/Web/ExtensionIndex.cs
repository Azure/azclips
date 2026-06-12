// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Web
{
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_deleteWebappOrchestrator_PS = new(
            Name: "DeleteWebappProcessor_Orchestrator",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Web.Processors.DeleteWebappProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Web",
            ProcessorType: typeof(Processors.DeleteWebappProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", typeof(string), "The name of the web app.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["DeleteMetrics"] = new("DeleteMetrics", typeof(bool), "If true, web app metrics are also deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DeleteEmptyServerFarm"] = new("DeleteEmptyServerFarm", typeof(bool), "Specify false if you want to keep empty App Service plan. By default, empty App Service plan is deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Slot"] = new("Slot", typeof(string), "Name of the deployment slot to delete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_deleteWebappOrchestrator_CLI = new(
            Name: "DeleteWebappProcessor_Orchestrator",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Web.Processors.DeleteWebappProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Web",
            ProcessorType: typeof(Processors.DeleteWebappProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", typeof(string), "The name of the web app.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["delete-metrics"] = new("DeleteMetrics", typeof(bool), "If true, web app metrics are also deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["delete-empty-server-farm"] = new("DeleteEmptyServerFarm", typeof(bool), "Specify false if you want to keep empty App Service plan. By default, empty App Service plan is deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["slot"] = new("Slot", typeof(string), "Name of the deployment slot to delete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Remove-Webapp"]                = s_deleteWebappOrchestrator_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["delete"]   = s_deleteWebappOrchestrator_CLI,
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
