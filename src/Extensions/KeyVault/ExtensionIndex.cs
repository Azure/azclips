using System.Collections.ObjectModel;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault;

[ExtensionVerb("keyvault")]
public class ExtensionIndex : IExtensionIndex
{
    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_commands =
        new ReadOnlyDictionary<string, CommandMetadata>(new Dictionary<string, CommandMetadata>());

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        var table = GetCommandTable(clientType);
        return table.TryGetValue(name, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
    }

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => clientType switch
    {
        ClientType.CLI or ClientType.PowerShell => s_commands,
        _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
    };

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetCommandTable(clientType).Values;

    public IEnumerable<string> GetCommandKeys(ClientType clientType) => GetCommandTable(clientType).Keys;
}