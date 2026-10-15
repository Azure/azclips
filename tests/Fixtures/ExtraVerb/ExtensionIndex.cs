// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Azclips.Tests.Fixtures.ExtraVerb;

/// <summary>
/// Test fixture: the conventional <c>ExtensionIndex</c> declares <c>primary</c>. A decorated
/// secondary <c>IExtensionIndex</c> implementer (declared first) and a decorated non-index type
/// must both be ignored: the runtime only loads <c>&lt;assembly name&gt;.ExtensionIndex</c>.
/// </summary>
[ExtensionVerb("other")]
public sealed class SecondaryIndex : IExtensionIndex
{
    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        throw new KeyNotFoundException(name);

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) =>
        new Dictionary<string, CommandMetadata>();

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) =>
        Array.Empty<CommandMetadata>();

    public IEnumerable<string> GetCommandKeys(ClientType clientType) =>
        clientType == ClientType.CLI ? new[] { "other run" } : Array.Empty<string>();
}

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
