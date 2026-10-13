// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Azclips.Tests.Fixtures.ExtraVerb;

/// <summary>
/// Test fixture: a valid <c>IExtensionIndex</c> declaring <c>primary</c>, plus a second
/// <c>[ExtensionVerb]</c> type that does not implement the interface. Runtime discovery
/// (<c>ExtensionVerbReader</c>) reports both verbs, so <c>LeafReader.ReadContract</c> must too.
/// </summary>
[ExtensionVerb("primary")]
public sealed class ExtensionIndex : IExtensionIndex
{
    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        throw new KeyNotFoundException(name);

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) =>
        new Dictionary<string, CommandMetadata>();

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) =>
        Array.Empty<CommandMetadata>();

    public IEnumerable<string> GetCommandKeys(ClientType clientType) =>
        clientType == ClientType.CLI ? new[] { "primary run" } : Array.Empty<string>();
}

[ExtensionVerb("secondary")]
public sealed class AdditionalDeclaration
{
}
