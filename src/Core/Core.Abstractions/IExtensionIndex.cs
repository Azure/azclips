// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Abstractions
{
    // Yabo: Should these interfaces be in SharedContract or merge SharedContract to Core.Abstraction?
    public interface IExtensionIndex
    {
        ICommandMetadata GetCommandMetaData(string name, ClientType clientType);

        /// <summary>
        /// Returns all registered commands for the given client type.
        /// Used by the help system to enumerate available commands.
        /// </summary>
        IEnumerable<ICommandMetadata> GetAllCommands(ClientType clientType);
    }
}
