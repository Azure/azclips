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
    }
}
