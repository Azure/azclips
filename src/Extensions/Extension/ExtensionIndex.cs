using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension;

[ExtensionIndex]
public partial class ExtensionIndex : IExtensionIndex
{
    private readonly IDictionary<string, ICommandMetadata> _psTable = new Dictionary<string, ICommandMetadata>();
    private readonly IDictionary<string, ICommandMetadata> _cliTable = new Dictionary<string, ICommandMetadata>();

    public ExtensionIndex()
    {
        FillCliTable(_cliTable);
    }

    public ICommandMetadata GetCommandMetaData(string name, ClientType clientType)
    {
        switch (clientType)
        {
            case ClientType.PowerShell:
                if (_psTable.TryGetValue(name, out var psData))
                {
                    return psData;
                }
                break;
            case ClientType.CLI:
                if (_cliTable.TryGetValue(name, out var cliData))
                {
                    return cliData;
                }
                break;
        }
        throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
    }

    public IEnumerable<ICommandMetadata> GetAllCommands(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.PowerShell => _psTable.Values,
            ClientType.CLI => _cliTable.Values,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
    }
}

class CommandMetadata : ICommandMetadata
{
    public string Name { get; }
    public string FullName { get; }
    public string AssemblyName { get; }
    public Type CommandType { get; }

    public CommandMetadata(string name, string fullName, string assemblyName, Type commandType)
    {
        Name = name;
        FullName = fullName;
        AssemblyName = assemblyName;
        CommandType = commandType;
    }
}