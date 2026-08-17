// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage;

[ExtensionVerb("storage")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private const string AssemblyName = "Microsoft.Azure.Azclips.Extensions.Storage";

    private static readonly CommandMetadata s_uploadBlob = new(
        Name: nameof(Processors.UploadBlobProcessor),
        InvocationName: "storage blob upload",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.UploadBlobProcessor",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.UploadBlobProcessor),
        Parameters: BlobParameters(),
        ParameterAliases: new Dictionary<string, string>
        {
            ["c"] = "container-name",
            ["n"] = "name",
            ["f"] = "file",
        });

    private static readonly CommandMetadata s_downloadBlob = new(
        Name: nameof(Processors.DownloadBlobProcessor),
        InvocationName: "storage blob download",
        Aliases: [],
        FullName: $"{AssemblyName}.Processors.DownloadBlobProcessor",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(Processors.DownloadBlobProcessor),
        Parameters: BlobParameters(),
        ParameterAliases: new Dictionary<string, string>
        {
            ["c"] = "container-name",
            ["n"] = "name",
            ["f"] = "file",
        });

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["storage blob upload"] = s_uploadBlob,
        ["storage blob download"] = s_downloadBlob,
    };

    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_psTable =
        new Dictionary<string, CommandMetadata>();

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

    private static Dictionary<string, ParameterMetadata> BlobParameters() => new()
    {
        ["account-name"] = new("AccountName", "account-name", typeof(string), "Storage account name.", true, [], []),
        ["container-name"] = new("ContainerName", "container-name", typeof(string), "Blob container name.", true, ["c"], []),
        ["name"] = new("BlobName", "name", typeof(string), "Blob name.", true, ["n"], []),
        ["file"] = new("FilePath", "file", typeof(string), "Local file path.", true, ["f"], []),
        ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Overwrite an existing destination.", false, [], []),
        ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile for this command.", false, [], []),
    };
}
