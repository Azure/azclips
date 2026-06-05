// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Rest
{
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_rest_PS = new(
            Name: "RestProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Rest.Processors.RestProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Rest",
            ProcessorType: typeof(Processors.RestProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Method"]  = new("Method", typeof(string), "HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS). Defaults to GET.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Uri"]     = new("Uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters inputs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Payload"] = new("Payload", typeof(string), "Optional JSON request body for POST/PUT/PATCH.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Headers"] = new("Headers", typeof(List<string>), "Optional request headers in KEY=VALUE form.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubscriptionId"]       = new("SubscriptionId", typeof(string), "Subscription id used to build an ARM URL from piece-wise parameters. Defaults to the current subscription. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]    = new("ResourceGroupName", typeof(string), "Resource group name segment for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceProviderName"] = new("ResourceProviderName", typeof(string), "Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceType"]         = new("ResourceType", typeof(List<string>), "Resource type segments, interleaved with Name, for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                 = new("Name", typeof(List<string>), "Resource name segments, interleaved with ResourceType (may have one fewer entry for a collection GET). Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ApiVersion"]           = new("ApiVersion", typeof(string), "api-version for a piece-wise ARM URL. Required when building from parameters. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UriParameters"]        = new("UriParameters", typeof(List<string>), "Optional query-string parameters in KEY=VALUE form, appended to the final URL. Allowed in both URI and ByParameters modes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_rest_CLI = new(
            Name: "RestProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Rest.Processors.RestProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Rest",
            ProcessorType: typeof(Processors.RestProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["method"]  = new("Method", typeof(string), "HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS). Defaults to GET.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["uri"]     = new("Uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters flags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["body"]    = new("Payload", typeof(string), "Optional JSON request body for POST/PUT/PATCH.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["headers"] = new("Headers", typeof(List<string>), "Optional request headers in KEY=VALUE form (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription"]      = new("SubscriptionId", typeof(string), "Subscription id used to build an ARM URL from piece-wise flags. Defaults to the current subscription. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]    = new("ResourceGroupName", typeof(string), "Resource group name segment for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-provider"] = new("ResourceProviderName", typeof(string), "Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-type"]     = new("ResourceType", typeof(List<string>), "Resource type segments (space-separated), interleaved with --name, for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]              = new("Name", typeof(List<string>), "Resource name segments (space-separated), interleaved with --resource-type (may have one fewer entry for a collection GET). Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["api-version"]       = new("ApiVersion", typeof(string), "api-version for a piece-wise ARM URL. Required when building from flags. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["uri-parameters"]    = new("UriParameters", typeof(List<string>), "Optional query-string parameters in KEY=VALUE form (space-separated), appended to the final URL. Allowed in both --uri and ByParameters modes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["body"] = "Payload",
                ["subscription"] = "SubscriptionId",
                ["resource-group"] = "ResourceGroupName",
                ["resource-provider"] = "ResourceProviderName",
                ["resource-type"] = "ResourceType",
                ["api-version"] = "ApiVersion",
                ["uri-parameters"] = "UriParameters",
            }
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Invoke-CirrusRestMethod"] = s_rest_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            // rest is a special command: the command group word itself is the verb (azv2 rest ...).
            [""] = s_rest_CLI,
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
