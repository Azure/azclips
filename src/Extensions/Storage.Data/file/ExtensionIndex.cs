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

        private static readonly CommandMetadata cliStorageDirectoryCreate = new(
            Name: nameof(CreateDirectoryProcessor_Orchestrator),
            InvocationName: "storage directory create",
            Aliases: [],
            FullName: typeof(CreateDirectoryProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateDirectoryProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateDirectoryProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The directory URL. Alternative to --account-name, --share-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and directory path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["name"] = new("DirectoryPath", "name", typeof(string), "The directory path within the share, e.g. parent/child. The parent must exist.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageDirectoryShow = new(
            Name: nameof(GetPropertiesDirectoryProcessor_Orchestrator),
            InvocationName: "storage directory show",
            Aliases: [],
            FullName: typeof(GetPropertiesDirectoryProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(GetPropertiesDirectoryProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(GetPropertiesDirectoryProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The directory URL. Alternative to --account-name, --share-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and directory path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["name"] = new("DirectoryPath", "name", typeof(string), "The directory path within the share, e.g. parent/child.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "The share snapshot to query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageFileList = new(
            Name: nameof(ListFilesAndDirectoriesSegmentDirectoryProcessor),
            InvocationName: "storage file list",
            Aliases: [],
            FullName: typeof(ListFilesAndDirectoriesSegmentDirectoryProcessor).FullName!,
            AssemblyName: typeof(ListFilesAndDirectoriesSegmentDirectoryProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(ListFilesAndDirectoriesSegmentDirectoryProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The URL of the share or directory to list. Alternative to --account-name, --share-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["name"] = new("DirectoryPath", "name", typeof(string), "The directory path to list, e.g. parent/child. Lists the share root when not given.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Sharesnapshot", "snapshot", typeof(string), "The share snapshot to list.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["prefix"] = new("Prefix", "prefix", typeof(string), "Filters the results to return only entries whose name begins with the specified prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["marker"] = new("Marker", "marker", typeof(string), "An opaque string value that identifies the portion of the result set to return with this operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["maxresults"] = new("Maxresults", "maxresults", typeof(int), "Specifies the maximum number of entries, directories and files combined, the service scans per page.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include"] = new("Include", "include", typeof(string[]), "Specify to include additional, optional information.", IsRequired: false, Aliases: [], AllowedValues: ["Timestamps", "Etag", "Attributes", "PermissionKey", "Permissions", "LinkCount", "NfsAttributes", "All"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageDirectoryDelete = new(
            Name: nameof(DeleteDirectoryProcessor),
            InvocationName: "storage directory delete",
            Aliases: [],
            FullName: typeof(DeleteDirectoryProcessor).FullName!,
            AssemblyName: typeof(DeleteDirectoryProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(DeleteDirectoryProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The directory URL. Alternative to --account-name, --share-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and directory path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["name"] = new("DirectoryPath", "name", typeof(string), "The directory path within the share, e.g. parent/child. The directory must be empty.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliStorageFileShow = new(
            Name: nameof(GetPropertiesFileProcessor_Orchestrator),
            InvocationName: "storage file show",
            Aliases: [],
            FullName: typeof(GetPropertiesFileProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(GetPropertiesFileProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(GetPropertiesFileProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The file URL. Alternative to --account-name, --share-name and --path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and file path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["path"] = new("FilePath", "path", typeof(string), "The file path within the share, e.g. dir/file.txt.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "The share snapshot to query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliStorageFileCreate = new(
            Name: nameof(CreateFileProcessor_Orchestrator),
            InvocationName: "storage file create",
            Aliases: [],
            FullName: typeof(CreateFileProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateFileProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateFileProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The file URL. Alternative to --account-name, --share-name and --path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and file path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["path"] = new("FilePath", "path", typeof(string), "The file path within the share, e.g. dir/file.txt. The directory must exist; an existing file is replaced.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["size"] = new("Size", "size", typeof(long), "The file size in bytes. The file is created empty, filled with zeros.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-type"] = new("ContentType", "content-type", typeof(string), "The content type of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-encoding"] = new("ContentEncoding", "content-encoding", typeof(string), "The content encoding of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-language"] = new("ContentLanguage", "content-language", typeof(string), "The content language of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-disposition"] = new("ContentDisposition", "content-disposition", typeof(string), "The content disposition of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-cache-control"] = new("CacheControl", "content-cache-control", typeof(string), "The cache control of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliStorageFileUpload = new(
            Name: nameof(UploadFileProcessor_Orchestrator),
            InvocationName: "storage file upload",
            Aliases: [],
            FullName: typeof(UploadFileProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(UploadFileProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(UploadFileProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The file URL. Alternative to --account-name, --share-name and --path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and file path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["path"] = new("FilePath", "path", typeof(string), "The destination file path within the share, e.g. dir/file.txt. The directory must exist; an existing file is replaced.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source"] = new("Source", "source", typeof(string), "Path of the local file to upload.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-type"] = new("ContentType", "content-type", typeof(string), "The content type of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-encoding"] = new("ContentEncoding", "content-encoding", typeof(string), "The content encoding of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-language"] = new("ContentLanguage", "content-language", typeof(string), "The content language of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-disposition"] = new("ContentDisposition", "content-disposition", typeof(string), "The content disposition of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-cache-control"] = new("CacheControl", "content-cache-control", typeof(string), "The cache control of the file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliStorageFileDownload = new(
            Name: nameof(DownloadFileProcessor_Orchestrator),
            InvocationName: "storage file download",
            Aliases: [],
            FullName: typeof(DownloadFileProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(DownloadFileProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(DownloadFileProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The file URL. Alternative to --account-name, --share-name and --path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and file path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["path"] = new("FilePath", "path", typeof(string), "The file path within the share, e.g. dir/file.txt.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dest"] = new("Destination", "dest", typeof(string), "Path of the local file to write. An existing file is not overwritten.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliStorageFileCopyStart = new(
            Name: nameof(StartCopyFileProcessor_Orchestrator),
            InvocationName: "storage file copy start",
            Aliases: [],
            FullName: typeof(StartCopyFileProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(StartCopyFileProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(StartCopyFileProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The destination file URL. Alternative to --account-name, --share-name and --path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and file path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["path"] = new("FilePath", "path", typeof(string), "The destination file path within the share, e.g. dir/file.txt.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-uri"] = new("SourceUrl", "source-uri", typeof(string), "The source file or blob URL. It must be in the destination account or carry a SAS.", IsRequired: false, Aliases: ["u"], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Destination metadata in space-separated key=value pairs. The source metadata is copied when not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata cliStorageFileDelete = new(
            Name: nameof(DeleteFileProcessor),
            InvocationName: "storage file delete",
            Aliases: [],
            FullName: typeof(DeleteFileProcessor).FullName!,
            AssemblyName: typeof(DeleteFileProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(DeleteFileProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The file URL. Alternative to --account-name, --share-name and --path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the share name and file path when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "The share name.", IsRequired: false, Aliases: ["s"], AllowedValues: []),
                ["path"] = new("FilePath", "path", typeof(string), "The file path within the share, e.g. dir/file.txt.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
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
            cliStorageShareDelete,
            cliStorageDirectoryCreate,
            cliStorageDirectoryShow,
            cliStorageDirectoryDelete,
            cliStorageFileList,
            cliStorageFileShow,
            cliStorageFileCreate,
            cliStorageFileUpload,
            cliStorageFileDownload,
            cliStorageFileCopyStart,
            cliStorageFileDelete
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
