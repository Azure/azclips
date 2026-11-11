// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.StorageData.Blobs;

[ExtensionVerb("storage")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly CommandMetadata s_blob_CLI = new(
        Name: nameof(BlobProcessor),
        InvocationName: "storage blob",
        Aliases: [],
        FullName: typeof(BlobProcessor).FullName!,
        AssemblyName: typeof(BlobProcessor).Assembly.GetName().Name!,
        ProcessorType: typeof(BlobProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>(),
        ParameterAliases: new Dictionary<string, string>());

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["storage blob"] = s_blob_CLI,
    };

    private static readonly Dictionary<string, CommandMetadata> s_psTable = new();

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        GetCommandTable(clientType).TryGetValue(name, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) =>
        clientType switch
        {
            ClientType.CLI => s_cliTable,
            ClientType.PowerShell => s_psTable,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) =>
        GetCommandTable(clientType).Values;

    public IEnumerable<string> GetCommandKeys(ClientType clientType) =>
        GetCommandTable(clientType).Keys;
}
