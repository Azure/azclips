// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage;

[ExtensionVerb("storage")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private const string AssemblyName = "Microsoft.Azure.Azclips.Extensions.Storage";

    private static readonly CommandMetadata s_uploadBlob = new(
        Name: nameof(Processors.BlockBlobUploadProcessor_Orchestrator),
        InvocationName: "storage blob upload",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.BlockBlobUploadProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.BlockBlobUploadProcessor_Orchestrator),
        Parameters: UploadBlobParameters(),
        ParameterAliases: new Dictionary<string, string>
        {
            ["c"] = "container-name",
            ["n"] = "name",
            ["f"] = "file",
        });

    private static readonly CommandMetadata s_downloadBlob = new(
        Name: nameof(Processors.BlobDownloadProcessor_Orchestrator),
        InvocationName: "storage blob download",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.BlobDownloadProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.BlobDownloadProcessor_Orchestrator),
        Parameters: DownloadBlobParameters(),
        ParameterAliases: new Dictionary<string, string>
        {
            ["c"] = "container-name",
            ["n"] = "name",
            ["f"] = "file",
        });

    private static readonly CommandMetadata s_uploadBlobBatch = new(
        Name: nameof(Processors.BlobUploadBatchProcessor_Orchestrator),
        InvocationName: "storage blob upload-batch",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.BlobUploadBatchProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.BlobUploadBatchProcessor_Orchestrator),
        Parameters: UploadBlobBatchParameters(),
        ParameterAliases: new Dictionary<string, string>
        {
            ["d"] = "destination",
            ["s"] = "source",
        });

    private static readonly CommandMetadata s_downloadBlobBatch = new(
        Name: nameof(Processors.BlobDownloadBatchProcessor_Orchestrator),
        InvocationName: "storage blob download-batch",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.BlobDownloadBatchProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.BlobDownloadBatchProcessor_Orchestrator),
        Parameters: DownloadBlobBatchParameters(),
        ParameterAliases: new Dictionary<string, string>
        {
            ["d"] = "destination",
            ["s"] = "source",
        });

    private static readonly CommandMetadata s_uploadBlobPs = new(
        Name: nameof(Processors.BlockBlobUploadProcessor_Orchestrator),
        InvocationName: "Set-AzStorageBlobContent",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.BlockBlobUploadProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.BlockBlobUploadProcessor_Orchestrator),
        Parameters: UploadBlobParametersPowerShell(),
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_downloadBlobPs = new(
        Name: nameof(Processors.BlobDownloadProcessor_Orchestrator),
        InvocationName: "Get-AzStorageBlobContent",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.BlobDownloadProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.BlobDownloadProcessor_Orchestrator),
        Parameters: DownloadBlobParametersPowerShell(),
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_listBlob = BlobCommand(
        "storage blob list",
        typeof(Processors.ListStorageBlobProcessor),
        ListBlobParameters());

    private static readonly CommandMetadata s_showBlob = BlobCommand(
        "storage blob show",
        typeof(Processors.ShowStorageBlobProcessor),
        NamedBlobParameters());

    private static readonly CommandMetadata s_existsBlob = BlobCommand(
        "storage blob exists",
        typeof(Processors.BlobExistsProcessor),
        NamedBlobParameters());

    private static readonly CommandMetadata s_listBlobPs = BlobCommand(
        "Get-AzStorageBlob_List",
        typeof(Processors.ListStorageBlobProcessor),
        ListBlobParametersPowerShell());

    private static readonly CommandMetadata s_showBlobPs = BlobCommand(
        "Get-AzStorageBlob_Get",
        typeof(Processors.ShowStorageBlobProcessor),
        NamedBlobParametersPowerShell());

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["storage blob upload"] = s_uploadBlob,
        ["storage blob download"] = s_downloadBlob,
        ["storage blob upload-batch"] = s_uploadBlobBatch,
        ["storage blob download-batch"] = s_downloadBlobBatch,
        ["storage blob list"] = s_listBlob,
        ["storage blob show"] = s_showBlob,
        ["storage blob exists"] = s_existsBlob,
    };

    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_psTable =
        new Dictionary<string, CommandMetadata>
        {
            ["Set-AzStorageBlobContent"] = s_uploadBlobPs,
            ["Get-AzStorageBlobContent"] = s_downloadBlobPs,
            ["Get-AzStorageBlob_List"] = s_listBlobPs,
            ["Get-AzStorageBlob_Get"] = s_showBlobPs,
        };

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        IReadOnlyDictionary<string, CommandMetadata> table = GetCommandTable(clientType);
        return table.TryGetValue(name, out CommandMetadata? metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
    }

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) =>
        clientType switch
        {
            ClientType.CLI => s_cliTable,
            ClientType.PowerShell => s_psTable,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported."),
        };

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) =>
        GetCommandTable(clientType).Values;

    public IEnumerable<string> GetCommandKeys(ClientType clientType) =>
        GetCommandTable(clientType).Keys;

    private static CommandMetadata BlobCommand(
        string invocationName,
        Type processorType,
        Dictionary<string, ParameterMetadata> parameters) => new(
            Name: processorType.Name,
            InvocationName: invocationName,
            Aliases: [],
            FullName: processorType.FullName!,
            AssemblyName: AssemblyName,
            ProcessorType: processorType,
            Parameters: parameters,
            ParameterAliases: new Dictionary<string, string>
            {
                ["c"] = "container-name",
                ["n"] = "name",
            });

    private static Dictionary<string, ParameterMetadata> UploadBlobParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", true, [], []),
        ["container-name"] = new("ContainerName", "container-name", typeof(string), "Blob container name.", true, ["c"], []),
        ["name"] = new("BlobName", "name", typeof(string), "Blob name. Defaults to the file name.", false, ["n"], []),
        ["file"] = new("FilePath", "file", typeof(string), "Local file path.", true, ["f"], []),
        ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Overwrite an existing destination.", false, [], []),
        ["blob-type"] = new("BlobType", "blob-type", typeof(string), "Blob type.", false, [], ["Block", "Page", "Append"]),
        ["metadata"] = new("Metadata", "metadata", typeof(string[]), "Metadata in KEY=VALUE form.", false, [], []),
        ["tags"] = new("Tags", "tags", typeof(string[]), "Blob tags in KEY=VALUE form.", false, [], []),
        ["properties"] = new("Properties", "properties", typeof(string[]), "Blob properties in KEY=VALUE form.", false, [], []),
        ["tier"] = new("StandardBlobTier", "tier", typeof(string), "Block blob access tier.", false, [], ["Hot", "Cool", "Cold", "Archive"]),
        ["premium-tier"] = new("PremiumPageBlobTier", "premium-tier", typeof(string), "Page blob premium tier.", false, [], []),
        ["encryption-scope"] = new("EncryptionScope", "encryption-scope", typeof(string), "Encryption scope.", false, [], []),
        ["tag-condition"] = new("TagCondition", "tag-condition", typeof(string), "Tag condition for the write.", false, [], []),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> DownloadBlobParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", false, [], []),
        ["container-name"] = new("ContainerName", "container-name", typeof(string), "Blob container name.", false, ["c"], []),
        ["name"] = new("BlobName", "name", typeof(string), "Blob name.", false, ["n"], []),
        ["file"] = new("FilePath", "file", typeof(string), "Local file or directory path.", false, ["f"], []),
        ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Overwrite an existing destination.", false, [], []),
        ["uri"] = new("AbsoluteUri", "uri", typeof(string), "Azure Blob Storage URL.", false, [], []),
        ["snapshot"] = new("SnapshotTime", "snapshot", typeof(string), "Blob snapshot timestamp.", false, [], []),
        ["version-id"] = new("VersionId", "version-id", typeof(string), "Blob version ID.", false, [], []),
        ["check-md5"] = new("CheckMd5", "check-md5", typeof(bool), "Validate the response MD5 checksum.", false, [], []),
        ["tag-condition"] = new("TagCondition", "tag-condition", typeof(string), "Tag condition for the read.", false, [], []),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> UploadBlobBatchParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", true, [], []),
        ["destination"] = new("ContainerName", "destination", typeof(string), "Destination blob container.", true, ["d"], []),
        ["source"] = new("SourceDirectory", "source", typeof(string), "Local source directory.", true, ["s"], []),
        ["destination-path"] = new("DestinationPath", "destination-path", typeof(string), "Blob path prefix.", false, [], []),
        ["pattern"] = new("Pattern", "pattern", typeof(string), "Glob pattern for files to upload.", false, [], []),
        ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Overwrite existing blobs.", false, [], []),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> DownloadBlobBatchParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", true, [], []),
        ["source"] = new("ContainerName", "source", typeof(string), "Source blob container.", true, ["s"], []),
        ["destination"] = new("DestinationDirectory", "destination", typeof(string), "Local destination directory.", true, ["d"], []),
        ["pattern"] = new("Pattern", "pattern", typeof(string), "Glob pattern for blobs to download.", false, [], []),
        ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Overwrite existing files.", false, [], []),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> NamedBlobParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", true, [], []),
        ["container-name"] = new("ContainerName", "container-name", typeof(string), "Blob container name.", true, ["c"], []),
        ["name"] = new("BlobName", "name", typeof(string), "Blob name.", true, ["n"], []),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> ListBlobParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", true, [], []),
        ["container-name"] = new("ContainerName", "container-name", typeof(string), "Blob container name.", true, ["c"], []),
        ["prefix"] = new("Prefix", "prefix", typeof(string), "Filter blobs by name prefix.", false, [], []),
        ["marker"] = new("Marker", "marker", typeof(string), "Continuation marker.", false, [], []),
        ["num-results"] = new("NumResults", "num-results", typeof(int), "Maximum number of blobs to return.", false, [], []),
        ["include"] = new("Include", "include", typeof(string[]), "Additional datasets to include.", false, [], [
            "copy", "deleted", "metadata", "snapshots", "uncommittedblobs", "versions", "tags",
            "immutabilitypolicy", "legalhold", "deletedwithversions"]),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> UploadBlobParametersPowerShell() => new()
    {
        ["AccountName"] = new("AccountName", "AccountName", typeof(string), "Storage account name.", true, [], []),
        ["ContainerName"] = new("ContainerName", "ContainerName", typeof(string), "Blob container name.", true, [], []),
        ["BlobName"] = new("BlobName", "BlobName", typeof(string), "Blob name. Defaults to the file name.", false, [], []),
        ["FilePath"] = new("FilePath", "FilePath", typeof(string), "Local file path.", true, [], []),
        ["Overwrite"] = new("Overwrite", "Overwrite", typeof(bool), "Overwrite an existing destination.", false, [], []),
        ["BlobType"] = new("BlobType", "BlobType", typeof(string), "Blob type.", false, [], ["Block", "Page", "Append"]),
        ["Metadata"] = new("Metadata", "Metadata", typeof(string[]), "Metadata in KEY=VALUE form.", false, [], []),
        ["Tags"] = new("Tags", "Tags", typeof(string[]), "Blob tags in KEY=VALUE form.", false, [], []),
        ["Properties"] = new("Properties", "Properties", typeof(string[]), "Blob properties in KEY=VALUE form.", false, [], []),
        ["StandardBlobTier"] = new("StandardBlobTier", "StandardBlobTier", typeof(string), "Block blob access tier.", false, [], ["Hot", "Cool", "Cold", "Archive"]),
        ["PremiumPageBlobTier"] = new("PremiumPageBlobTier", "PremiumPageBlobTier", typeof(string), "Page blob premium tier.", false, [], []),
        ["EncryptionScope"] = new("EncryptionScope", "EncryptionScope", typeof(string), "Encryption scope.", false, [], []),
        ["TagCondition"] = new("TagCondition", "TagCondition", typeof(string), "Tag condition for the write.", false, [], []),
        ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> DownloadBlobParametersPowerShell() => new()
    {
        ["AccountName"] = new("AccountName", "AccountName", typeof(string), "Storage account name.", false, [], []),
        ["ContainerName"] = new("ContainerName", "ContainerName", typeof(string), "Blob container name.", false, [], []),
        ["BlobName"] = new("BlobName", "BlobName", typeof(string), "Blob name.", false, [], []),
        ["FilePath"] = new("FilePath", "FilePath", typeof(string), "Local file or directory path.", false, [], []),
        ["Overwrite"] = new("Overwrite", "Overwrite", typeof(bool), "Overwrite an existing destination.", false, [], []),
        ["AbsoluteUri"] = new("AbsoluteUri", "AbsoluteUri", typeof(string), "Azure Blob Storage URL.", false, [], []),
        ["SnapshotTime"] = new("SnapshotTime", "SnapshotTime", typeof(string), "Blob snapshot timestamp.", false, [], []),
        ["VersionId"] = new("VersionId", "VersionId", typeof(string), "Blob version ID.", false, [], []),
        ["CheckMd5"] = new("CheckMd5", "CheckMd5", typeof(bool), "Validate the response MD5 checksum.", false, [], []),
        ["TagCondition"] = new("TagCondition", "TagCondition", typeof(string), "Tag condition for the read.", false, [], []),
        ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> NamedBlobParametersPowerShell() => new()
    {
        ["AccountName"] = new("AccountName", "AccountName", typeof(string), "Storage account name.", true, [], []),
        ["ContainerName"] = new("ContainerName", "ContainerName", typeof(string), "Blob container name.", true, [], []),
        ["BlobName"] = new("BlobName", "BlobName", typeof(string), "Blob name.", true, [], []),
        ["IncludeTag"] = new("IncludeTag", "IncludeTag", typeof(bool), "Include blob tags.", false, [], []),
        ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };

    private static Dictionary<string, ParameterMetadata> ListBlobParametersPowerShell() => new()
    {
        ["AccountName"] = new("AccountName", "AccountName", typeof(string), "Storage account name.", true, [], []),
        ["ContainerName"] = new("ContainerName", "ContainerName", typeof(string), "Blob container name.", true, [], []),
        ["Prefix"] = new("Prefix", "Prefix", typeof(string), "Filter blobs by name prefix.", false, [], []),
        ["Marker"] = new("Marker", "Marker", typeof(string), "Continuation marker.", false, [], []),
        ["NumResults"] = new("NumResults", "NumResults", typeof(int), "Maximum number of blobs to return.", false, [], []),
        ["Include"] = new("Include", "Include", typeof(string[]), "Additional datasets to include.", false, [], []),
        ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };
}
