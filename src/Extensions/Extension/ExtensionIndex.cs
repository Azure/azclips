// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Extension;

[ExtensionVerb("extension")]
public class ExtensionIndex : IExtensionIndex
{
    // ── CLI Command Metadata ──
    private static readonly CommandMetadata s_addExtension_CLI = new(
        Name: "AddExtensionProcessor",
        InvocationName: "extension add",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.AddExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.AddExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"]     = new("Name", "name", typeof(string), "Package id (full or short suffix) of the extension to add from a feed. Verb and namespace tokens are not accepted. Omit when using --path.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["version"]  = new("Version", "version", typeof(string), "Optional version to install when using --name. Omit to use the latest available package version.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["feed-url"] = new("FeedUrl", "feed-url", typeof(string), "NuGet feed URL to install from when using --name.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["path"]     = new("Path", "path", typeof(string), "Path to a local extension .nupkg file to install.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["pin"]      = new("Pin", "pin", typeof(bool), "Pin the installed version so future 'update' commands skip it without --force.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["force"]    = new("Force", "force", typeof(bool), "Allow add to overwrite an existing pinned installation of the same package.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listExtensions_CLI = new(
        Name: "ListExtensionsProcessor",
        InvocationName: "extension list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.ListExtensionsProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.ListExtensionsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["by-verb"] = new("ByVerb", "by-verb", typeof(bool), "Regroup output verb-headed: one section per verb, listing every package that contributes to it.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_showExtension_CLI = new(
        Name: "ShowExtensionProcessor",
        InvocationName: "extension show",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.ShowExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.ShowExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", "name", typeof(string), "Package id (full or short suffix) of the extension to show. Verb and namespace tokens are not accepted.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        // Empty-key alias lets dispatcher-bound positional residual (e.g. `extension show Compute`) bind to Name.
        ParameterAliases: new Dictionary<string, string> { [""] = "name" }
    );

    private static readonly CommandMetadata s_removeExtension_CLI = new(
        Name: "RemoveExtensionProcessor",
        InvocationName: "extension remove",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.RemoveExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.RemoveExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", "name", typeof(string), "Package id (full or short suffix) of the extension to remove. Verb and namespace tokens are not accepted.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "name" }
    );

    private static readonly CommandMetadata s_updateExtension_CLI = new(
        Name: "UpdateExtensionProcessor",
        InvocationName: "extension update",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.UpdateExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.UpdateExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"]     = new("Name", "name", typeof(string), "Package id (full or short suffix) of the extension to update. Verb and namespace tokens are not accepted.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["version"]  = new("Version", "version", typeof(string), "Target version. Defaults to the latest available from the configured feed.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["feed-url"] = new("FeedUrl", "feed-url", typeof(string), "Feed URL to install the new version from.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["force"]    = new("Force", "force", typeof(bool), "Update even if the extension is pinned.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "name" }
    );

    private static readonly CommandMetadata s_pinExtension_CLI = new(
        Name: "PinExtensionProcessor",
        InvocationName: "extension pin",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.PinExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.PinExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", "name", typeof(string), "Package id (full or short suffix) of the extension to pin. Verb and namespace tokens are not accepted.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "name" }
    );

    private static readonly CommandMetadata s_unpinExtension_CLI = new(
        Name: "UnpinExtensionProcessor",
        InvocationName: "extension unpin",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Extension.Processors.UnpinExtensionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Extension",
        ProcessorType: typeof(Processors.UnpinExtensionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", "name", typeof(string), "Package id (full or short suffix) of the extension to unpin. Verb and namespace tokens are not accepted.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string> { [""] = "name" }
    );
    // ── Dispatch Tables ──
    private static readonly Dictionary<string, CommandMetadata> s_psTable = new();
    // Full-path keys (verb + sub-command).
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["extension add"]    = s_addExtension_CLI,
        ["extension list"]   = s_listExtensions_CLI,
        ["extension show"]   = s_showExtension_CLI,
        ["extension remove"] = s_removeExtension_CLI,
        ["extension update"] = s_updateExtension_CLI,
        ["extension pin"]    = s_pinExtension_CLI,
        ["extension unpin"]  = s_unpinExtension_CLI,
    };
    static ExtensionIndex()
    {
        s_cliTable.AddDefaultParameterSets();
    }

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