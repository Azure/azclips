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

        private static readonly CommandMetadata cliStorageBlobShow = new(
            Name: nameof(GetPropertiesBlobProcessor),
            InvocationName: "storage blob show",
            Aliases: [],
            FullName: typeof(GetPropertiesBlobProcessor).FullName!,
            AssemblyName: typeof(GetPropertiesBlobProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(GetPropertiesBlobProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The blob URL.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "Specifies the snapshot of the blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["version-id"] = new("VersionId", "version-id", typeof(string), "Specifies the version ID of the blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "If specified, the operation only succeeds if the resource's lease is active and matches this ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key"] = new("EncryptionKey", "encryption-key", typeof(string), "Specifies the encryption key to use to encrypt the data provided in the request.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-sha256"] = new("EncryptionKeySha256", "encryption-key-sha256", typeof(string), "The SHA-256 hash of the provided encryption key. Must be provided if the encryption key is provided.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-algorithm"] = new("EncryptionAlgorithm", "encryption-algorithm", typeof(string), "The algorithm used to produce the encryption key hash. Must be provided if the encryption key is provided.", IsRequired: false, Aliases: [], AllowedValues: ["AES256"]),
                ["if-modified-since"] = new("IfModifiedSince", "if-modified-since", typeof(string), "Specify this value to operate only on a blob if it has been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSince", "if-unmodified-since", typeof(string), "Specify this value to operate only on a blob if it has not been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "Specify this value to operate only on a blob with a matching Etag value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "Specify this value to operate only on a blob with a non-matching Etag value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-tags"] = new("IfTags", "if-tags", typeof(string), "Specifies a SQL-like where clause on blob tags to operate only on a blob with matching tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        #endregion

        #region Command Tables
        private static readonly Dictionary<string, CommandMetadata> psTable = new();

        private static readonly Dictionary<string, CommandMetadata> cliTable = BuildCommandTable(
            cliStorageBlobList,
            cliStorageBlobShow
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
