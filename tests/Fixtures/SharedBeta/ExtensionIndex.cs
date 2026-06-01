// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Cirrus.Tests.Fixtures.SharedBeta;

/// <summary>
/// Test fixture: declares the verb <c>sharedverb</c> (same as SharedAlpha) and a single leaf
/// <c>sharedverb beta</c>. Pair with SharedAlpha to verify shared-verb co-existence.
/// </summary>
[ExtensionVerb("sharedverb")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["sharedverb beta"] = new CommandMetadata(
            Name: "BetaProcessor",
            Aliases: [],
            FullName: "Cirrus.Tests.Fixtures.SharedBeta.BetaProcessor",
            AssemblyName: "Cirrus.Tests.Fixtures.SharedBeta",
            ProcessorType: typeof(ExtensionIndex),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()),
    };

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        clientType == ClientType.CLI && s_cliTable.TryGetValue(name, out var md)
            ? md
            : throw new KeyNotFoundException(name);

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) =>
        clientType == ClientType.CLI ? s_cliTable.Values : Array.Empty<CommandMetadata>();

    public IEnumerable<string> GetCommandKeys(ClientType clientType) =>
        clientType == ClientType.CLI ? s_cliTable.Keys : Array.Empty<string>();
}
