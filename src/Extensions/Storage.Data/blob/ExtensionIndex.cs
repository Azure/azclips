// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob
{
    [ExtensionVerb("storage")]
    public class ExtensionIndex : IExtensionIndex
    {
        #region CLI Command Metadata
        private static readonly CommandMetadata cliStorageBlobList = new(
            Name: nameof(ListBlobsContainerProcessor),
            InvocationName: "storage blob list",
            Aliases: [],
            FullName: typeof(ListBlobsContainerProcessor).FullName!,
            AssemblyName: typeof(ListBlobsContainerProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(ListBlobsContainerProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The container URL.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["prefix"] = new("Prefix", "prefix", typeof(string), "Filters the results to return only resources whose name begins with the specified prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["marker"] = new("Marker", "marker", typeof(string), "An opaque string value that identifies the portion of the result set to return with this operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["maxresults"] = new("Maxresults", "maxresults", typeof(int), "Specifies the maximum number of resources to return.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include"] = new("Include", "include", typeof(string[]), "Specify to include additional, optional information.", IsRequired: false, Aliases: [], AllowedValues: ["copy", "deleted", "metadata", "snapshots", "uncommittedblobs", "versions", "tags", "immutabilitypolicy", "legalhold", "deletedwithversions"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["start-from"] = new("StartFrom", "start-from", typeof(string), "Specifies the relative path to list paths from.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        #endregion

        #region Command Tables
        private static readonly Dictionary<string, CommandMetadata> psTable = new();

        private static readonly Dictionary<string, CommandMetadata> cliTable = BuildCommandTable(
            cliStorageBlobList
        );

        private static Dictionary<string, CommandMetadata> BuildCommandTable(params CommandMetadata[] commands) =>
            commands.ToDictionary(cmd => cmd.InvocationName, cmd => cmd);
        #endregion

        #region Interface Implementation
        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            return GetCommandTable(clientType).TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => psTable,
                ClientType.CLI => cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return GetCommandTable(clientType).Keys;
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return GetCommandTable(clientType).Values;
        }
        #endregion
    }
}
