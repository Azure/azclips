using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Config;

public class ExtensionIndex : IExtensionIndex
{
    private static readonly CommandMetadata s_getConfig_CLI = new(
        Name: "GetConfigCommand",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Config.Commands.GetConfigCommand",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Config",
        CommandType: typeof(Commands.GetConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["json"] = new("Json", typeof(bool), "Output as JSON.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_setConfig_CLI = new(
        Name: "SetConfigCommand",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Config.Commands.SetConfigCommand",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Config",
        CommandType: typeof(Commands.SetConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["value"] = new("Value", typeof(string), "Configuration value.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["force"] = new("Force", typeof(bool), "Allow unknown keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_deleteConfig_CLI = new(
        Name: "DeleteConfigCommand",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Config.Commands.DeleteConfigCommand",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Config",
        CommandType: typeof(Commands.DeleteConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["force"] = new("Force", typeof(bool), "Allow unknown keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listConfig_CLI = new(
        Name: "ListConfigCommand",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Config.Commands.ListConfigCommand",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Config",
        CommandType: typeof(Commands.ListConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["all"] = new("All", typeof(bool), "Include default values.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["json"] = new("Json", typeof(bool), "Output as JSON.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_infoConfig_CLI = new(
        Name: "InfoConfigCommand",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Config.Commands.InfoConfigCommand",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Config",
        CommandType: typeof(Commands.InfoConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["keys-only"] = new("KeysOnly", typeof(bool), "Return only valid keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly Dictionary<string, CommandMetadata> s_psTable = new();
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["get"] = s_getConfig_CLI,
        ["set"] = s_setConfig_CLI,
        ["delete"] = s_deleteConfig_CLI,
        ["list"] = s_listConfig_CLI,
        ["info"] = s_infoConfig_CLI,
    };

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