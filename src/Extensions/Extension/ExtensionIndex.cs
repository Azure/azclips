using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension;

public class ExtensionIndex : IExtensionIndex
{
    private readonly IDictionary<string, CommandMetadata> _psTable = new Dictionary<string, CommandMetadata>();
    private readonly IDictionary<string, CommandMetadata> _cliTable = new Dictionary<string, CommandMetadata>();

    public ExtensionIndex()
    {
        _cliTable.Add("add", new CommandMetadata(
            Name: nameof(Commands.AddExtensionCommand),
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Commands.AddExtensionCommand",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
            CommandType: typeof(Commands.AddExtensionCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()
        ));
    }

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
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

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.PowerShell => _psTable.Values,
            ClientType.CLI => _cliTable.Values,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
    }
}
