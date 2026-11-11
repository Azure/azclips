// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Processors;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile
{
    [ExtensionVerb("storage")]
    public class ExtensionIndex : IExtensionIndex
    {
        #region CLI Command Metadata
        private static readonly CommandMetadata cliStorageShareCreate = new(
            Name: nameof(CreateShareProcessor_Orchestrator),
            InvocationName: "storage share create",
            Aliases: [],
            FullName: typeof(CreateShareProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateShareProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateShareProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The share URL. Alternative to --account-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ShareName", "name", typeof(string), "The share name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["quota"] = new("Quota", "quota", typeof(int), "The maximum size of the share in GiB.", IsRequired: false, Aliases: ["q"], AllowedValues: []),
                ["access-tier"] = new("AccessTier", "access-tier", typeof(string), "The access tier of the share.", IsRequired: false, Aliases: [], AllowedValues: ["TransactionOptimized", "Hot", "Cool", "Premium"]),
                ["enabled-protocols"] = new("EnabledProtocols", "enabled-protocols", typeof(string), "The protocol to enable for the share.", IsRequired: false, Aliases: [], AllowedValues: ["SMB", "NFS"]),
                                ["root-squash"] = new("RootSquash", "root-squash", typeof(string), "Root squashing behavior on the share. Only valid for NFS shares.", IsRequired: false, Aliases: [], AllowedValues: ["NoRootSquash", "RootSquash", "AllSquash"]),                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageShareShow = new(
            Name: nameof(GetPropertiesShareProcessor_Orchestrator),
            InvocationName: "storage share show",
            Aliases: [],
            FullName: typeof(GetPropertiesShareProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(GetPropertiesShareProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(GetPropertiesShareProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The share URL. Alternative to --account-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ShareName", "name", typeof(string), "The share name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "The share snapshot to query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "If specified, the operation only succeeds if the share's lease is active and matches this ID.", IsRequired: false, Aliases: [], AllowedValues: []),                
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageShareList = new(
            Name: nameof(ListSharesSegmentServiceProcessor),
            InvocationName: "storage share list",
            Aliases: [],
            FullName: typeof(ListSharesSegmentServiceProcessor).FullName!,
            AssemblyName: typeof(ListSharesSegmentServiceProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(ListSharesSegmentServiceProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The file service URL. Alternative to --account-name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["prefix"] = new("Prefix", "prefix", typeof(string), "Filters the results to return only shares whose name begins with the specified prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["marker"] = new("Marker", "marker", typeof(string), "An opaque string value that identifies the portion of the result set to return with this operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["maxresults"] = new("Maxresults", "maxresults", typeof(int), "Specifies the maximum number of shares to return.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include"] = new("Include", "include", typeof(string[]), "Specify to include additional, optional information.", IsRequired: false, Aliases: [], AllowedValues: ["snapshots", "metadata", "deleted"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageShareUpdate = new(
            Name: nameof(SetPropertiesShareProcessor),
            InvocationName: "storage share update",
            Aliases: [],
            FullName: typeof(SetPropertiesShareProcessor).FullName!,
            AssemblyName: typeof(SetPropertiesShareProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(SetPropertiesShareProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The share URL. Alternative to --account-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ShareName", "name", typeof(string), "The share name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["quota"] = new("Quota", "quota", typeof(int), "The maximum size of the share in GiB.", IsRequired: false, Aliases: ["q"], AllowedValues: []),
                ["access-tier"] = new("AccessTier", "access-tier", typeof(string), "The access tier of the share.", IsRequired: false, Aliases: [], AllowedValues: ["TransactionOptimized", "Hot", "Cool", "Premium"]),
                ["root-squash"] = new("RootSquash", "root-squash", typeof(string), "Root squashing behavior on the share. Only valid for NFS shares.", IsRequired: false, Aliases: [], AllowedValues: ["NoRootSquash", "RootSquash", "AllSquash"]),
                                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "If specified, the operation only succeeds if the share's lease is active and matches this ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageShareDelete = new(
            Name: nameof(DeleteShareProcessor),
            InvocationName: "storage share delete",
            Aliases: [],
            FullName: typeof(DeleteShareProcessor).FullName!,
            AssemblyName: typeof(DeleteShareProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(DeleteShareProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The share URL. Alternative to --account-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ShareName", "name", typeof(string), "The share name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Sharesnapshot", "snapshot", typeof(string), "Delete only this share snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["delete-snapshots"] = new("DeleteSnapshots", "delete-snapshots", typeof(string), "Also delete the share's snapshots. Required when the share has snapshots.", IsRequired: false, Aliases: [], AllowedValues: ["include", "include-leased"]),
                                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "If specified, the operation only succeeds if the share's lease is active and matches this ID.", IsRequired: false, Aliases: [], AllowedValues: []),                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        #endregion

        #region Command Tables
        private static readonly Dictionary<string, CommandMetadata> psTable = new();

        private static readonly Dictionary<string, CommandMetadata> cliTable = BuildCommandTable(
            cliStorageShareCreate,
            cliStorageShareShow,
            cliStorageShareList,
            cliStorageShareUpdate,
            cliStorageShareDelete
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
