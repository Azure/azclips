// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    // Yabo: Should these interfaces be in SharedContract or merge SharedContract to Core.Abstraction?
    public interface IExtensionIndex
    {
        CommandMetadata GetCommandMetadata(string name, ClientType clientType);

        /// <summary>
        /// Returns all registered commands for the given client type,
        /// keyed by their dispatch name (e.g. CLI subcommand or PS cmdlet name).
        /// </summary>
        IEnumerable<KeyValuePair<string, CommandMetadata>> GetAllCommands(ClientType clientType);
    }
}
