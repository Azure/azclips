// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    // Yabo: Should these interfaces be in SharedContract or merge SharedContract to Core.Abstraction?
    public interface IExtensionIndex
    {
        CommandMetadata GetCommandMetadata(string name, ClientType clientType);

        /// <summary>
        /// Returns the keyed command table for the given client type.
        /// Keys are the canonical command identifiers used by that client surface.
        /// </summary>
        IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType);

        /// <summary>
        /// Returns all registered commands for the given client type.
        /// Used by the help system to enumerate available commands.
        /// </summary>
        IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType);

        /// <summary>
        /// Returns all command-table keys (e.g. <c>"vm create"</c>) for the given client type.
        /// Used at <c>extension add</c> time to enumerate the extension's contributed leaves
        /// without executing any processor code. Default implementation returns an empty
        /// enumerable for back-compat; in-tree extensions override to expose
        /// <c>s_cliTable.Keys</c>.
        /// </summary>
        IEnumerable<string> GetCommandKeys(ClientType clientType) => Array.Empty<string>();

        /// <summary>
        /// Returns the CLI verbs this extension contributes (e.g. <c>"vm"</c>, <c>"disk"</c> for
        /// the Compute extension). Mirrors the <c>[ExtensionVerb]</c> attribute declarations on
        /// the index type. The help system uses this to project one root group per verb so
        /// multi-verb extensions surface each verb as its own user-facing namespace.
        /// Default returns empty for back-compat; in-tree extensions override.
        /// </summary>
        IReadOnlyList<string> GetVerbs() => Array.Empty<string>();
    }
}
