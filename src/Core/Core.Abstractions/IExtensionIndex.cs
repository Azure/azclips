// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    // Yabo: Should these interfaces be in SharedContract or merge SharedContract to Core.Abstraction?
    public interface IExtensionIndex
    {
        CommandMetadata GetCommandMetadata(string name, ClientType clientType);

        /// <summary>
        /// Returns all registered commands for the given client type.
        /// Used by the help system to enumerate available commands.
        /// </summary>
        IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType);

        /// <summary>
        /// Returns all command-table keys (e.g. <c>"vm create"</c>) for the given client type.
        /// Used at <c>extension add</c> time to enumerate the extension's contributed leaves
        /// (per <c>docs/spec/extension-management.md §11.7</c>) without executing any processor
        /// code. Default implementation returns an empty enumerable for back-compat; in-tree
        /// extensions override to expose <c>s_cliTable.Keys</c>.
        /// </summary>
        IEnumerable<string> GetCommandKeys(ClientType clientType) => Array.Empty<string>();
    }
}
