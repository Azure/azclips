// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage;

[ExtensionVerb("storage")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_cliTable = BuildCliTable();
    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_psTable = BuildPowerShellTable();

    private static IReadOnlyDictionary<string, CommandMetadata> BuildCliTable()
    {
        var table = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account keys list"] = Command<ListStorageAccountKeysCommand>("storage account keys list",
                [
                    Parameter("ResourceGroupName", "resource-group", typeof(string), "Storage account resource group.", true, ["g"]),
                    Parameter("AccountName", "account-name", typeof(string), "Storage account name.", true, ["n"]),
                    Parameter("SubscriptionId", "subscription-id", typeof(string), "ARM subscription ID. Defaults to the selected profile."),
                    Parameter("Profile", "profile", typeof(string), "Use the named connection profile."),
                    Parameter("ExpandKeyType", "expand-key-type", typeof(string), "Include Kerberos keys with kerb.", allowedValues: ["kerb"])
                ])
        };
        AddBlob<ShowStorageBlobCommand>("show",
            [Parameter("IncludeTags", "include-tags", typeof(bool), "Include tags using an ETag-guarded tag lookup.")]);
        AddBlob<TestStorageBlobExistsCommand>("exists", []);
        AddBlob<RemoveStorageBlobCommand>("delete",
            [Parameter("DeleteSnapshots", "delete-snapshots", typeof(string), "Delete the base and snapshots, or snapshots only.",
                allowedValues: ["include", "only"])]);
        AddBlob<SetStorageBlobTierCommand>("set-tier",
            [
                Parameter("Tier", "tier", typeof(string), "Requested Block/Page access tier.", true,
                    allowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                Parameter("BlobType", "type", typeof(string), "Blob type: block (default) or page.", aliases: ["t"],
                    allowedValues: ["block", "page"]),
                Parameter("RehydratePriority", "rehydrate-priority", typeof(string), "Block blob rehydration priority.", aliases: ["r"],
                    allowedValues: ["High", "Standard"])
            ], conditions: false);
        table.Add("storage blob list", Command<ListStorageBlobsCommand>("storage blob list",
            [
                .. BlobAddressParameters(),
                Parameter("Prefix", "prefix", typeof(string), "Return names beginning with this literal prefix."),
                Parameter("Delimiter", "delimiter", typeof(string), "Nonempty hierarchy delimiter; omit or use empty for flat listing."),
                Parameter("Include", "include", typeof(string), "Combined flags: c copy, d deleted, m metadata, s snapshots, t tags, v versions."),
                Parameter("Marker", "marker", typeof(string), "Opaque continuation marker from the preceding listing."),
                Parameter("NumResults", "num-results", typeof(string), "Positive 64-bit result limit (default 5000), or * for all; service pages are at most 5000."),
                Parameter("ShowNextMarker", "show-next-marker", typeof(bool), "Append a nextMarker object (including null) to the result array.")
            ]));
        table.Add("storage blob copy start", Command<StartStorageBlobCopyCommand>("storage blob copy start",
            [
                .. BlobAddressParameters("destination-container"),
                Parameter("Blob", "destination-blob", typeof(string), "Exact destination blob; existing destination is overwritten unless conditioned.", true, ["b"]),
                Parameter("SourceUri", "source-uri", typeof(string), "Public Azure Blob/Files source URI, optionally containing a read SAS.", aliases: ["u"]),
                Parameter("SourceSas", "source-sas", typeof(string), "Existing read SAS for the source only; never included in output."),
                Parameter("SourceAccountKey", "source-account-key", typeof(string), "Source Files account key used only to sign a short-lived read SAS; never persisted or returned."),
                Parameter("UseSourceAccountKeyLookup", "use-source-account-key-lookup", typeof(bool), "Explicitly authorize ARM lookup of the source Files account key; never an automatic fallback."),
                Parameter("SourceKeyResourceGroup", "source-key-resource-group", typeof(string), "Required source resource group when ARM key lookup is selected."),
                Parameter("SourceKeySubscriptionId", "source-key-subscription-id", typeof(string), "Source ARM subscription; defaults to the source profile subscription."),
                Parameter("SourceKeyName", "source-key-name", typeof(string), "Source key name for explicit ARM lookup; defaults to key1."),
                Parameter("SourceStorageAccountName", "source-account-name", typeof(string), "Source account; defaults to destination for named sources."),
                Parameter("SourceContainer", "source-container", typeof(string), "Named Blob source container."),
                Parameter("SourceBlob", "source-blob", typeof(string), "Named Blob source name."),
                Parameter("SourceSnapshot", "source-snapshot", typeof(string), "Blob source snapshot; cannot be combined with source-version-id."),
                Parameter("SourceVersionId", "source-version-id", typeof(string), "Blob source version."),
                Parameter("SourceShareName", "source-share", typeof(string), "Named Files source share."),
                Parameter("SourcePath", "source-path", typeof(string), "Named Files source path."),
                Parameter("SourceShareSnapshot", "source-share-snapshot", typeof(string), "Files source share snapshot."),
                Parameter("EnableSourceFileBackupRequestIntent", "enable-source-file-backup-request-intent", typeof(bool), "Files OAuth intent; does not authorize a normal-copy source without a service SAS or explicit account-key signing."),
                Parameter("LeaseId", "destination-lease-id", typeof(string), "Destination lease ID."),
                Parameter("IfMatch", "destination-if-match", typeof(string), "Destination ETag match condition."),
                Parameter("IfNoneMatch", "destination-if-none-match", typeof(string), "Destination non-match condition; * protects absent destinations."),
                Parameter("IfModifiedSinceInput", "destination-if-modified-since", typeof(string), "Destination modified-since timestamp."),
                Parameter("IfUnmodifiedSinceInput", "destination-if-unmodified-since", typeof(string), "Destination unmodified-since timestamp."),
                Parameter("IfTags", "destination-tags-condition", typeof(string), "Destination SQL-like tag condition."),
                Parameter("SourceIfMatch", "source-if-match", typeof(string), "Blob source ETag match condition."),
                Parameter("SourceIfNoneMatch", "source-if-none-match", typeof(string), "Blob source non-match condition."),
                Parameter("SourceIfModifiedSinceInput", "source-if-modified-since", typeof(string), "Blob source modified-since timestamp."),
                Parameter("SourceIfUnmodifiedSinceInput", "source-if-unmodified-since", typeof(string), "Blob source unmodified-since timestamp."),
                Parameter("SourceIfTags", "source-tags-condition", typeof(string), "Blob source SQL-like tag condition."),
                Parameter("MetadataInput", "metadata", typeof(string[]), "Space-separated key=value metadata; nonempty unique case-insensitive keys."),
                Parameter("TagsInput", "tags", typeof(string[]), "Space-separated key=value tags; bare keys mean empty values; unique case-sensitive keys."),
                Parameter("Tier", "tier", typeof(string), "Compatible Block/Page destination tier.",
                    allowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                Parameter("RehydratePriority", "rehydrate-priority", typeof(string), "Block-tier rehydration priority.",
                    allowedValues: ["High", "Standard"])
            ]));
        AddBlob<UploadStorageBlobCommand>("upload",
            [
                Parameter("File", "file", typeof(string), "Source file; name defaults to its filename.", true, ["f"]),
                Parameter("BlobType", "type", typeof(string), "Block (default), Page (inferred for .vhd), or Append.", aliases: ["t"],
                    allowedValues: ["block", "page", "append"]),
                Parameter("Overwrite", "overwrite", typeof(bool), "Replace existing content; false by default. Append otherwise appends."),
                .. TransferParameters(),
                Parameter("ContentType", "content-type", typeof(string), "Content MIME type; inferred from common filename extensions when omitted."),
                Parameter("ContentEncoding", "content-encoding", typeof(string), "Content encoding."),
                Parameter("ContentLanguage", "content-language", typeof(string), "Content language."),
                Parameter("ContentDisposition", "content-disposition", typeof(string), "Content disposition."),
                Parameter("CacheControl", "content-cache-control", typeof(string), "Content cache control."),
                Parameter("ContentMd5Input", "content-md5", typeof(string), "Base64-encoded stored Content-MD5."),
                Parameter("MetadataInput", "metadata", typeof(string[]), "Space-separated unique key=value metadata."),
                Parameter("TagsInput", "tags", typeof(string[]), "Space-separated unique tags; bare keys mean empty values."),
                Parameter("Tier", "tier", typeof(string), "Compatible Block/Page tier.",
                    allowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                Parameter("MaxSizeCondition", "maxsize-condition", typeof(long), "Maximum Append blob size in bytes.")
            ], nameRequired: false, selectors: false);
        AddBlob<DownloadStorageBlobCommand>("download",
            [
                Parameter("File", "file", typeof(string), "Destination file; omit for byte-only standard output.", aliases: ["f"]),
                Parameter("Overwrite", "overwrite", typeof(bool), "Overwrite the destination file; true by default."),
                .. TransferParameters(),
                Parameter("CheckMd5", "check-md5", typeof(bool), "Validate the stored whole-blob MD5 when available."),
                Parameter("StartRange", "start-range", typeof(long), "Inclusive first byte."),
                Parameter("EndRange", "end-range", typeof(long), "Inclusive last byte.")
            ]);
        table.Add("storage blob download-batch", Command<DownloadStorageBlobBatchCommand>("storage blob download-batch",
            [
                .. BlobAddressParameters("source", ["s"]),
                Parameter("Destination", "destination", typeof(string), "Existing destination directory.", true, ["d"]),
                Parameter("Pattern", "pattern", typeof(string), "Python fnmatch-compatible wildcard selection."),
                Parameter("Overwrite", "overwrite", typeof(bool), "Overwrite existing files; false by default."),
                Parameter("DryRun", "dryrun", typeof(bool), "Plan and report without writing local files."),
                .. TransferParameters(),
                Parameter("CheckMd5", "check-md5", typeof(bool), "Validate stored MD5 when available."),
                Parameter("LeaseId", "lease-id", typeof(string), "Lease ID."),
                Parameter("IfMatch", "if-match", typeof(string), "ETag match condition."),
                Parameter("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition."),
                Parameter("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp."),
                Parameter("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp."),
                Parameter("IfTags", "tags-condition", typeof(string), "SQL-like tag condition.")
            ]));
        StorageFileFrontendRegistration.AddCli(table);
        return table;

        ParameterMetadata[] TransferParameters() =>
        [
            Parameter("Concurrency", "max-connections", typeof(int), "Bounded concurrent workers, 1-4 (default 2)."),
            Parameter("ChunkSize", "chunk-size", typeof(int), "Bytes per chunk; at most 8 MiB (Page: 4 MiB)."),
            Parameter("ValidateContent", "validate-content", typeof(bool), "Validate each transferred chunk with MD5."),
            Parameter("NoProgress", "no-progress", typeof(bool), "Suppress transfer progress, not errors or warnings.")
        ];

        ParameterMetadata[] BlobAddressParameters(string containerInvocation = "container-name", string[]? containerAliases = null) =>
        [
            Parameter("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint."),
            Parameter("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path."),
            Parameter("Container", containerInvocation, typeof(string), "Container name.", true, containerAliases ?? ["c"]),
            Parameter("Profile", "profile", typeof(string), "Use the named connection profile."),
            Parameter("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", allowedValues: ["login"]),
            Parameter("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.")
        ];

        void AddBlob<T>(string verb, ParameterMetadata[] extra, bool conditions = true, bool nameRequired = true, bool selectors = true) where T : IProcessor
        {
            List<ParameterMetadata> parameters =
            [
                .. BlobAddressParameters(),
                Parameter("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", nameRequired, ["n"]),
                Parameter("LeaseId", "lease-id", typeof(string), "Lease ID."),
                Parameter("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.")
            ];
            if (selectors)
                parameters.AddRange(
                [
                    Parameter("Snapshot", "snapshot", typeof(string), "Select a snapshot; cannot be combined with version-id."),
                    Parameter("VersionId", "version-id", typeof(string), "Select a version; cannot be combined with snapshot.")
                ]);
            if (conditions)
                parameters.AddRange(
                [
                    Parameter("IfMatch", "if-match", typeof(string), "ETag match condition."),
                    Parameter("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition."),
                    Parameter("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp; UTC if no offset is given."),
                    Parameter("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp; UTC if no offset is given.")
                ]);
            parameters.AddRange(extra);
            string invocation = $"storage blob {verb}";
            table.Add(invocation, Command<T>(invocation, [.. parameters]));
        }
    }

    private static IReadOnlyDictionary<string, CommandMetadata> BuildPowerShellTable()
    {
        var table = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["New-AzStorageContext"] = Command<NewStorageContextProcessor>("New-AzStorageContext",
                [
                    Parameter("StorageAccountName", "StorageAccountName", typeof(string), "Public Azure storage account name.", true),
                    Parameter("Profile", "Profile", typeof(string), "Use the named connection profile."),
                    Parameter("UseConnectedAccount", "UseConnectedAccount", typeof(bool), "Use Entra authentication. Defaults to true; false is unsupported."),
                    Parameter("EnableFileBackupRequestIntent", "EnableFileBackupRequestIntent", typeof(bool), "Explicitly enable backup intent for Azure Files OAuth.")
                ]),
            ["Get-AzStorageAccountKey"] = Command<ListStorageAccountKeysProcessor>("Get-AzStorageAccountKey",
                [
                    Parameter("ResourceGroupName", "ResourceGroupName", typeof(string), "Storage account resource group.", true),
                    Parameter("AccountName", "Name", typeof(string), "Storage account name.", true, ["AccountName", "StorageAccountName"]),
                    Parameter("SubscriptionId", "SubscriptionId", typeof(string), "ARM subscription ID. Defaults to the selected profile."),
                    Parameter("Profile", "Profile", typeof(string), "Use the named connection profile."),
                    Parameter("ListKerbKey", "ListKerbKey", typeof(bool), "Include Kerberos keys.")
                ]),
            ["Get-AzStorageBlob"] = Command<GetStorageBlobsPowerShellCommand>("Get-AzStorageBlob",
                [
                    Parameter("Context", "Context", typeof(StorageContext), "Native public-Azure Entra context."),
                    Parameter("Container", "Container", typeof(string), "Container name."),
                    Parameter("ContainerObject", "ContainerObject", typeof(StorageBlobContainer), "Native container pipeline input."),
                    Parameter("InputObject", "InputObject", typeof(StorageBlob), "Native blob pipeline input."),
                    Parameter("Blob", "Blob", typeof(string), "Exact blob name."),
                    Parameter("Snapshot", "Snapshot", typeof(string), "UTC snapshot selector."),
                    Parameter("VersionId", "VersionId", typeof(string), "Version selector."),
                    Parameter("ListMode", "ListMode", typeof(bool), "Use flat listing instead of exact HEAD."),
                    Parameter("Prefix", "Prefix", typeof(string), "Literal listing prefix."),
                    Parameter("NameFilter", "NameFilter", typeof(Func<string, bool>), "PowerShell-compiled name filter."),
                    Parameter("MaxCount", "MaxCount", typeof(int), "Maximum scanned entries, before filtering; nonpositive means unlimited."),
                    Parameter("ContinuationToken", "ContinuationToken", typeof(StorageBlobContinuationToken), "Native listing continuation."),
                    Parameter("IncludeDeleted", "IncludeDeleted", typeof(bool), "Include deleted blobs when listing."),
                    Parameter("IncludeVersion", "IncludeVersion", typeof(bool), "Include versions when listing by prefix."),
                    Parameter("IncludeTag", "IncludeTag", typeof(bool), "Include blob tags."),
                    Parameter("IfTags", "IfTags", typeof(string), "Tag condition for exact lookup."),
                    Parameter("Timeout", "Timeout", typeof(int), "Positive per-request service timeout."),
                    Parameter("Profile", "Profile", typeof(string), "Use the named connection profile.")
                ]),
            ["Remove-AzStorageBlob"] = Command<RemoveStorageBlobPowerShellCommand>("Remove-AzStorageBlob",
                [
                    Parameter("Context", "Context", typeof(StorageContext), "Native public-Azure Entra context."),
                    Parameter("Container", "Container", typeof(string), "Container name."),
                    Parameter("Blob", "Blob", typeof(string), "Exact blob name; wildcards are not expanded."),
                    Parameter("ContainerObject", "ContainerObject", typeof(StorageBlobContainer), "Native container pipeline input."),
                    Parameter("InputObject", "InputObject", typeof(StorageBlob), "Native blob pipeline input."),
                    Parameter("Snapshot", "Snapshot", typeof(string), "UTC snapshot selector."),
                    Parameter("VersionId", "VersionId", typeof(string), "Version selector."),
                    Parameter("DeleteSnapshots", "DeleteSnapshots", typeof(string), "Include snapshots, or delete only snapshots.",
                        allowedValues: ["include", "only"]),
                    Parameter("PassThru", "PassThru", typeof(bool), "Emit true only after successful deletion."),
                    Parameter("IfTags", "IfTags", typeof(string), "Tag condition for deletion."),
                    Parameter("Timeout", "Timeout", typeof(int), "Positive per-request service timeout."),
                    Parameter("Profile", "Profile", typeof(string), "Use the named connection profile.")
                ])
        };
        StorageBlobPowerShellRegistration.AddPowerShell(table);
        StorageFileFrontendRegistration.AddPowerShell(table);
        return table;
    }

    private static ParameterMetadata Parameter(string property, string invocation, Type type, string description,
        bool required = false, string[]? aliases = null, string[]? allowedValues = null) =>
        new(property, invocation, type, description, required, aliases ?? [], allowedValues ?? []);

    private static CommandMetadata Command<T>(string invocation, ParameterMetadata[] parameters) where T : IProcessor =>
        new(typeof(T).Name, invocation, [], typeof(T).FullName!, typeof(T).Assembly.GetName().Name!, typeof(T),
            parameters.ToDictionary(parameter => parameter.InvocationName, StringComparer.OrdinalIgnoreCase),
            parameters.SelectMany(parameter => parameter.Aliases.Select(alias => (alias, parameter.InvocationName)))
                .ToDictionary(pair => pair.alias, pair => pair.InvocationName, StringComparer.OrdinalIgnoreCase));

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        GetCommandTable(clientType).TryGetValue(name, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => clientType switch
    {
        ClientType.CLI => s_cliTable,
        ClientType.PowerShell => s_psTable,
        _ => throw new ArgumentException($"Client type '{clientType}' is not supported.", nameof(clientType))
    };

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetCommandTable(clientType).Values;
    public IEnumerable<string> GetCommandKeys(ClientType clientType) => GetCommandTable(clientType).Keys;
}
