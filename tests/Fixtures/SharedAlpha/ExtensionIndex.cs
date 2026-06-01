// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Cirrus.Tests.Fixtures.SharedAlpha;

/// <summary>
/// Test fixture: declares the verb <c>sharedverb</c> and a single leaf
/// <c>sharedverb alpha</c>. Used by the shared-verb contract test to verify that two extensions
/// sharing a verb but contributing disjoint leaves can co-exist (paired with SharedBeta) and
/// that two extensions sharing the same <c>(verb, path)</c> tuple collide (paired with itself
/// staged under a different package id).
/// </summary>
[ExtensionVerb("sharedverb")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["sharedverb alpha"] = new CommandMetadata(
            Name: "AlphaProcessor",
            Aliases: [],
            FullName: "Cirrus.Tests.Fixtures.SharedAlpha.AlphaProcessor",
            AssemblyName: "Cirrus.Tests.Fixtures.SharedAlpha",
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
