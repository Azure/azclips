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
                ["url"] = new("Url", "url", typeof(string), "The container URL. Alternative to --account-name and --container-name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container (and blob) name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("ContainerName", "container-name", typeof(string), "The container name.", IsRequired: false, Aliases: ["c"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
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
            Name: nameof(GetPropertiesBlobProcessor_Orchestrator),
            InvocationName: "storage blob show",
            Aliases: [],
            FullName: typeof(GetPropertiesBlobProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(GetPropertiesBlobProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(GetPropertiesBlobProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The blob URL. Alternative to --account-name, --container-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container (and blob) name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("ContainerName", "container-name", typeof(string), "The container name.", IsRequired: false, Aliases: ["c"], AllowedValues: []),
                ["name"] = new("BlobName", "name", typeof(string), "The blob name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
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

        private static readonly CommandMetadata cliStorageBlobCopyStart = new(
            Name: nameof(StartCopyFromUrlBlobProcessor_Orchestrator),
            InvocationName: "storage blob copy start",
            Aliases: [],
            FullName: typeof(StartCopyFromUrlBlobProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(StartCopyFromUrlBlobProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(StartCopyFromUrlBlobProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The destination blob URL. Alternative to --account-name, --destination-container and --destination-blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Destination storage account name. Used with the container and blob name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Destination storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature for the destination. Alternative to --account-key; a source SAS goes in --source-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-container"] = new("ContainerName", "destination-container", typeof(string), "The destination container name.", IsRequired: false, Aliases: ["c"], AllowedValues: []),
                ["destination-blob"] = new("BlobName", "destination-blob", typeof(string), "The destination blob name.", IsRequired: false, Aliases: ["b"], AllowedValues: []),
                ["source-uri"] = new("SourceUrl", "source-uri", typeof(string), "The source blob URL. It must be public, in the destination account, or carry a SAS.", IsRequired: true, Aliases: ["u"], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Destination metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("Tags", "tags", typeof(string[]), "Destination tags in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tier"] = new("Tier", "tier", typeof(string), "The tier to be set on the destination blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rehydrate-priority"] = new("RehydratePriority", "rehydrate-priority", typeof(string), "The priority of the rehydration operation.", IsRequired: false, Aliases: [], AllowedValues: ["High", "Standard"]),
                ["destination-lease"] = new("LeaseId", "destination-lease", typeof(string), "The lease ID specified for the destination blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-modified-since"] = new("SourceIfModifiedSince", "source-if-modified-since", typeof(string), "Copy only if the source blob has been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-unmodified-since"] = new("SourceIfUnmodifiedSince", "source-if-unmodified-since", typeof(string), "Copy only if the source blob has not been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-match"] = new("SourceIfMatch", "source-if-match", typeof(string), "Copy only if the source blob ETag matches.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-none-match"] = new("SourceIfNoneMatch", "source-if-none-match", typeof(string), "Copy only if the source blob ETag does not match.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-tags-condition"] = new("SourceIfTags", "source-tags-condition", typeof(string), "A SQL-like where clause on the source blob's tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-modified-since"] = new("IfModifiedSince", "destination-if-modified-since", typeof(string), "Copy only if the destination blob has been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-unmodified-since"] = new("IfUnmodifiedSince", "destination-if-unmodified-since", typeof(string), "Copy only if the destination blob has not been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-match"] = new("IfMatch", "destination-if-match", typeof(string), "Copy only if the destination blob ETag matches.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-none-match"] = new("IfNoneMatch", "destination-if-none-match", typeof(string), "Copy only if the destination blob ETag does not match.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-tags-condition"] = new("IfTags", "destination-tags-condition", typeof(string), "A SQL-like where clause on the destination blob's tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageBlobUpload = new(
            Name: nameof(UploadBlockBlobProcessor_Orchestrator),
            InvocationName: "storage blob upload",
            Aliases: [],
            FullName: typeof(UploadBlockBlobProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(UploadBlockBlobProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(UploadBlockBlobProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The blob URL. Alternative to --account-name, --container-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container and blob name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("ContainerName", "container-name", typeof(string), "The container name.", IsRequired: false, Aliases: ["c"], AllowedValues: []),
                ["name"] = new("BlobName", "name", typeof(string), "The blob name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["file"] = new("FilePath", "file", typeof(string), "Path of the local file to upload. An existing blob is not overwritten.", IsRequired: true, Aliases: ["f"], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("Tags", "tags", typeof(string[]), "Tags in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-type"] = new("BlobContentType", "content-type", typeof(string), "The blob's content type.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-encoding"] = new("BlobContentEncoding", "content-encoding", typeof(string), "The blob's content encoding.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-language"] = new("BlobContentLanguage", "content-language", typeof(string), "The blob's content language.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-disposition"] = new("BlobContentDisposition", "content-disposition", typeof(string), "The blob's content disposition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-cache-control"] = new("BlobCacheControl", "content-cache-control", typeof(string), "The blob's cache control.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tier"] = new("Tier", "tier", typeof(string), "The tier to be set on the blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-scope"] = new("EncryptionScope", "encryption-scope", typeof(string), "The encryption scope used to encrypt the blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageBlobUploadFromUrl = new(
            Name: nameof(UploadBlobFromUrlBlockBlobProcessor_Orchestrator),
            InvocationName: "storage blob upload-from-url",
            Aliases: [],
            FullName: typeof(UploadBlobFromUrlBlockBlobProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(UploadBlobFromUrlBlockBlobProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(UploadBlobFromUrlBlockBlobProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The blob URL. Alternative to --account-name, --container-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container and blob name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature for the destination. Alternative to --account-key; a source SAS goes in --source-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("ContainerName", "container-name", typeof(string), "The container name.", IsRequired: false, Aliases: ["c"], AllowedValues: []),
                ["name"] = new("BlobName", "name", typeof(string), "The blob name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["source-uri"] = new("SourceUrl", "source-uri", typeof(string), "The source blob URL. It must be public, in the destination account, or carry a SAS. An existing destination blob is not overwritten.", IsRequired: true, Aliases: ["u"], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("Tags", "tags", typeof(string[]), "Tags in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-type"] = new("BlobContentType", "content-type", typeof(string), "The blob's content type.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-encoding"] = new("BlobContentEncoding", "content-encoding", typeof(string), "The blob's content encoding.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-language"] = new("BlobContentLanguage", "content-language", typeof(string), "The blob's content language.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-disposition"] = new("BlobContentDisposition", "content-disposition", typeof(string), "The blob's content disposition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-cache-control"] = new("BlobCacheControl", "content-cache-control", typeof(string), "The blob's cache control.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tier"] = new("Tier", "tier", typeof(string), "The tier to be set on the blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-scope"] = new("EncryptionScope", "encryption-scope", typeof(string), "The encryption scope used to encrypt the blob.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["copy-source-blob-properties"] = new("CopySourceBlobProperties", "copy-source-blob-properties", typeof(bool), "Whether the source blob's properties are copied. Defaults to true.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageBlobDelete = new(
            Name: nameof(DeleteBlobProcessor),
            InvocationName: "storage blob delete",
            Aliases: [],
            FullName: typeof(DeleteBlobProcessor).FullName!,
            AssemblyName: typeof(DeleteBlobProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(DeleteBlobProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The blob URL. Alternative to --account-name, --container-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container and blob name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("ContainerName", "container-name", typeof(string), "The container name.", IsRequired: false, Aliases: ["c"], AllowedValues: []),
                ["name"] = new("BlobName", "name", typeof(string), "The blob name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["delete-snapshots"] = new("DeleteSnapshots", "delete-snapshots", typeof(string), "Required if the blob has snapshots. 'include' deletes the blob and its snapshots; 'only' deletes just the snapshots.", IsRequired: false, Aliases: [], AllowedValues: ["include", "only"]),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "The snapshot of the blob to delete.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["version-id"] = new("VersionId", "version-id", typeof(string), "The version ID of the blob to delete.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["delete-type"] = new("BlobDeleteType", "delete-type", typeof(string), "'Permanent' permanently deletes a soft-deleted snapshot or version, if permanent delete is enabled on the account.", IsRequired: false, Aliases: [], AllowedValues: ["Permanent"]),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "If specified, the operation only succeeds if the blob's lease is active and matches this ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSince", "if-modified-since", typeof(string), "Delete only if the blob has been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSince", "if-unmodified-since", typeof(string), "Delete only if the blob has not been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "Delete only if the blob's ETag matches this value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "Delete only if the blob's ETag does not match this value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "A SQL where clause on blob tags; delete only if it matches.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageContainerCreate = new(
            Name: nameof(CreateContainerProcessor_Orchestrator),
            InvocationName: "storage container create",
            Aliases: [],
            FullName: typeof(CreateContainerProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateContainerProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateContainerProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The container URL. Alternative to --account-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ContainerName", "name", typeof(string), "The container name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in space-separated key=value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-access"] = new("PublicAccess", "public-access", typeof(string), "The public access level. Omit for private access.", IsRequired: false, Aliases: [], AllowedValues: ["blob", "container"]),
                ["default-encryption-scope"] = new("DefaultEncryptionScope", "default-encryption-scope", typeof(string), "The default encryption scope for all writes to the container.", IsRequired: false, Aliases: ["d"], AllowedValues: []),
                ["prevent-encryption-scope-override"] = new("PreventEncryptionScopeOverride", "prevent-encryption-scope-override", typeof(bool), "Block writes that specify an encryption scope other than the container default.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata cliStorageContainerDelete = new(
            Name: nameof(DeleteContainerProcessor),
            InvocationName: "storage container delete",
            Aliases: [],
            FullName: typeof(DeleteContainerProcessor).FullName!,
            AssemblyName: typeof(DeleteContainerProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(DeleteContainerProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["url"] = new("Url", "url", typeof(string), "The container URL. Alternative to --account-name and --name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Used with the container name when --url is not given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-key"] = new("StorageAccountKey", "account-key", typeof(string), "Storage account key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-token"] = new("SasToken", "sas-token", typeof(string), "A shared access signature. Alternative to --account-key.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ContainerName", "name", typeof(string), "The container name.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "If specified, the operation only succeeds if the container's lease is active and matches this ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSince", "if-modified-since", typeof(string), "Delete only if the container has been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSince", "if-unmodified-since", typeof(string), "Delete only if the container has not been modified since the specified date-time.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "The timeout parameter is expressed in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["client-request-id"] = new("ClientRequestId", "client-request-id", typeof(string), "An opaque, globally-unique, client-generated string identifier for the request.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        #endregion

        #region Command Tables
        private static readonly Dictionary<string, CommandMetadata> psTable = new();

        private static readonly Dictionary<string, CommandMetadata> cliTable = BuildCommandTable(
            cliStorageBlobList,
            cliStorageBlobShow,
            cliStorageBlobCopyStart,
            cliStorageBlobUpload,
            cliStorageBlobUploadFromUrl,
            cliStorageBlobDelete,
            cliStorageContainerCreate,
            cliStorageContainerDelete
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
