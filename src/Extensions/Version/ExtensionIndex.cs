// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Version;

[ExtensionVerb("version")]
public class ExtensionIndex : IExtensionIndex
{
    // ── CLI Command Metadata ──
    private static readonly CommandMetadata s_getVersion_CLI = new(
        Name: "GetVersionProcessor",
        InvocationName: "version",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Version.Processors.GetVersionProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.Version",
        ProcessorType: typeof(Processors.GetVersionProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>(),
        ParameterAliases: new Dictionary<string, string>()
    );

    // ── Dispatch Tables ──
    // Version is a CLI-only built-in (no PowerShell module), so the PS table is empty —
    // mirroring the `extension` built-in. The interface branches below still honor the
    // ClientType contract.
    private static readonly Dictionary<string, CommandMetadata> s_psTable = new();

    // `az version` dispatches like any other core built-in (e.g. `az login`, `az config`).
    // The verb keys as just "version" (bare-verb core pattern) and resolves through the standard
    // built-in seed in BuiltInExtensions.Map. The CLI output formatter serializes the returned
    // object to JSON.
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["version"] = s_getVersion_CLI,
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
        return clientType switch
        {
            ClientType.PowerShell => s_psTable.Values,
            ClientType.CLI => s_cliTable.Values,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };
    }

    private static readonly IReadOnlyList<string> s_verbs = ["version"];
    public IReadOnlyList<string> GetVerbs() => s_verbs;
}
