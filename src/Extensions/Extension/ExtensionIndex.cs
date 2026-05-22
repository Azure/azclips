using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension;

[ExtensionVerb("extension")]
public class ExtensionIndex : IExtensionIndex
{
    // ── CLI Command Metadata ──
    private static readonly CommandMetadata s_addExtension_CLI = new(
        Name: "AddExtensionProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.AddExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.AddExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"]     = new("Name", typeof(string), "The name of the extension to add.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["version"]  = new("Version", typeof(string), "The version of the extension to add.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["feed-url"] = new("FeedUrl", typeof(string), "Feed URL to install the extension from.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["path"]     = new("Path", typeof(string), "Path to the extension .nupkg file to add.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["pin"]      = new("Pin", typeof(bool), "Pin the installed version so future 'update' commands skip it without --force.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listExtensions_CLI = new(
        Name: "ListExtensionsProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.ListExtensionsProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.ListExtensionsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>(),
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_showExtension_CLI = new(
        Name: "ShowExtensionProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.ShowExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.ShowExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", typeof(string), "Package id or verb of the extension to show.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeExtension_CLI = new(
        Name: "RemoveExtensionProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.RemoveExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.RemoveExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", typeof(string), "Package id or verb of the extension to remove.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_updateExtension_CLI = new(
        Name: "UpdateExtensionProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.UpdateExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.UpdateExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"]     = new("Name", typeof(string), "Package id or verb of the extension to update.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["version"]  = new("Version", typeof(string), "Target version. Defaults to the latest available from the configured feed.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["feed-url"] = new("FeedUrl", typeof(string), "Feed URL to install the new version from.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["force"]    = new("Force", typeof(bool), "Update even if the extension is pinned.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_pinExtension_CLI = new(
        Name: "PinExtensionProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.PinExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.PinExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", typeof(string), "Package id or verb of the extension to pin.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_unpinExtension_CLI = new(
        Name: "UnpinExtensionProcessor",
        Aliases: [],
        FullName: "Microsoft.Azure.Cirrus.Extensions.Extension.Processors.UnpinExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Extension",
        ProcessorType: typeof(Processors.UnpinExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", typeof(string), "Package id or verb of the extension to unpin.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );
    // ── Dispatch Tables ──
    private static readonly Dictionary<string, CommandMetadata> s_psTable = new();
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["add"]    = s_addExtension_CLI,
        ["list"]   = s_listExtensions_CLI,
        ["show"]   = s_showExtension_CLI,
        ["remove"] = s_removeExtension_CLI,
        ["update"] = s_updateExtension_CLI,
        ["pin"]    = s_pinExtension_CLI,
        ["unpin"]  = s_unpinExtension_CLI,
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