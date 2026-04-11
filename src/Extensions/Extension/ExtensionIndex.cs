using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension;

public class ExtensionIndex : IExtensionIndex
{
    // ── CLI Command Metadata ──
    private static readonly CommandMetadata s_addExtension_CLI = new(
        Name: "AddExtensionCommand",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Commands.AddExtensionCommand",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        CommandType: typeof(Commands.AddExtensionCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"]     = new("Name", typeof(string), "The name of the extension to add.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["version"]  = new("Version", typeof(string), "The version of the extension to add.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["feed-url"] = new("FeedUrl", typeof(string), "Feed URL to install the extension from.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["path"]     = new("Path", typeof(string), "Path to the extension .nupkg file to add.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );
    // ── Dispatch Tables ──
    private static readonly Dictionary<string, CommandMetadata> s_psTable = new();
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["add"] = s_addExtension_CLI,
    };
    // ── Lookup ──
    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        var table = clientType switch
        {
            ClientType.PowerShell => s_psTable,
            ClientType.CLI => s_cliTable,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
        return table.TryGetValue(name, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
    }
    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.PowerShell => s_psTable.Values,
            ClientType.CLI => s_cliTable.Values,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
    }
}