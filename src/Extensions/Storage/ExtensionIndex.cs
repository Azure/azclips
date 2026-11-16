// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage
{
    /// <summary>
    /// The assembly's index (the type the loader resolves as <c>{AssemblyName}.ExtensionIndex</c>).
    /// Composes the per-service indexes, which stay as each service's own generated index.
    /// </summary>
    [ExtensionVerb("storage")]
    public class ExtensionIndex : IExtensionIndex
    {
        private static readonly IExtensionIndex[] s_services =
        [
            new Management.ExtensionIndex(),
            new Microsoft.Azure.AzClips.Extensions.StorageBlob.ExtensionIndex(),
            new Microsoft.Azure.AzClips.Extensions.StorageFile.ExtensionIndex(),
        ];

        private static readonly Dictionary<string, CommandMetadata> s_psTable = Compose(ClientType.PowerShell);
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = Compose(ClientType.CLI);

        private static Dictionary<string, CommandMetadata> Compose(ClientType clientType)
        {
            var table = new Dictionary<string, CommandMetadata>();
            foreach (var service in s_services)
            {
                foreach (var (name, metadata) in service.GetCommandTable(clientType))
                {
                    if (!table.TryAdd(name, metadata))
                    {
                        throw new InvalidOperationException($"Command '{name}' is declared by more than one Storage service index for client type '{clientType}'.");
                    }
                }
            }
            return table;
        }

        private static Dictionary<string, CommandMetadata> GetTable(ClientType clientType) => clientType switch
        {
            ClientType.PowerShell => s_psTable,
            ClientType.CLI => s_cliTable,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
            GetTable(clientType).TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => GetTable(clientType);

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetTable(clientType).Values;

        public IEnumerable<string> GetCommandKeys(ClientType clientType) => GetTable(clientType).Keys;
    }
}