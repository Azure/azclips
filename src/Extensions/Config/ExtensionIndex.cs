// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config;

[ExtensionVerb("config")]
public class ExtensionIndex : IExtensionIndex
{
    // ── PS Command Metadata ──
    private static readonly CommandMetadata s_getConfig_PS = new(
        Name: "GetConfigCommand",
        InvocationName: "Get-AzConfig",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.GetConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.GetConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["Key"] = new("Key", "Key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_setConfig_PS = new(
        Name: "SetConfigCommand",
        InvocationName: "Set-AzConfig",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.SetConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.SetConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["Key"] = new("Key", "Key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Value"] = new("Value", "Value", typeof(string), "Configuration value.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Force"] = new("Force", "Force", typeof(bool), "Allow unknown keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeConfig_PS = new(
        Name: "RemoveConfigCommand",
        InvocationName: "Remove-AzConfig",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.DeleteConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.DeleteConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["Key"] = new("Key", "Key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Force"] = new("Force", "Force", typeof(bool), "Allow unknown keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listConfig_PS = new(
        Name: "ListConfigCommand",
        InvocationName: "Get-AzConfigList",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.ListConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.ListConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["All"] = new("All", "All", typeof(bool), "Include default values.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_infoConfig_PS = new(
        Name: "InfoConfigCommand",
        InvocationName: "Get-AzConfigInfo",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.InfoConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.InfoConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["KeysOnly"] = new("KeysOnly", "KeysOnly", typeof(bool), "Return only valid keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    // ── CLI Command Metadata ──
    private static readonly CommandMetadata s_getConfig_CLI = new(
        Name: "GetConfigCommand",
        InvocationName: "config get",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.GetConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.GetConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "key" }
    );

    private static readonly CommandMetadata s_setConfig_CLI = new(
        Name: "SetConfigCommand",
        InvocationName: "config set",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.SetConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.SetConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["value"] = new("Value", "value", typeof(string), "Configuration value.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["force"] = new("Force", "force", typeof(bool), "Allow unknown keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "key", ["1"] = "value" }
    );

    private static readonly CommandMetadata s_deleteConfig_CLI = new(
        Name: "DeleteConfigCommand",
        InvocationName: "config delete",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.DeleteConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.DeleteConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key in dot notation.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["force"] = new("Force", "force", typeof(bool), "Allow unknown keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "key" }
    );

    private static readonly CommandMetadata s_listConfig_CLI = new(
        Name: "ListConfigCommand",
        InvocationName: "config list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.ListConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.ListConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["all"] = new("All", "all", typeof(bool), "Include default values.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_infoConfig_CLI = new(
        Name: "InfoConfigCommand",
        InvocationName: "config info",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Config.Commands.InfoConfigCommand",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Config",
        ProcessorType: typeof(Commands.InfoConfigCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["keys-only"] = new("KeysOnly", "keys-only", typeof(bool), "Return only valid keys.", IsRequired: false, Aliases: [], AllowedValues: [])
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
    {
        ["Get-AzConfig"] = s_getConfig_PS,
        ["Set-AzConfig"] = s_setConfig_PS,
        ["Remove-AzConfig"] = s_removeConfig_PS,
        ["Get-AzConfigList"] = s_listConfig_PS,
        ["Get-AzConfigInfo"] = s_infoConfig_PS,
    };
    // Full-path keys.
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["config get"]    = s_getConfig_CLI,
        ["config set"]    = s_setConfig_CLI,
        ["config delete"] = s_deleteConfig_CLI,
        ["config list"]   = s_listConfig_CLI,
        ["config info"]   = s_infoConfig_CLI,
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

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.PowerShell => s_psTable,
            ClientType.CLI => s_cliTable,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
    }
    public IEnumerable<string> GetCommandKeys(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.PowerShell => s_psTable.Keys,
            ClientType.CLI => s_cliTable.Keys,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
    }

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
    {
        return GetCommandTable(clientType).Values;
    }
}