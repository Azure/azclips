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
                ["Uri"]     = new("Uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Payload"] = new("Payload", typeof(string), "Optional JSON request body for POST/PUT/PATCH.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Headers"] = new("Headers", typeof(List<string>), "Optional request headers in KEY=VALUE form.", IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["uri"]     = new("Uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["body"]    = new("Payload", typeof(string), "Optional JSON request body for POST/PUT/PATCH.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["headers"] = new("Headers", typeof(List<string>), "Optional request headers in KEY=VALUE form (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["body"] = "Payload",
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
