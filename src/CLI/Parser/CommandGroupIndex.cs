// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Cli.Parser
{
    /// <summary>
    /// Entry describing a top-level command group: group name → extension name + description.
    /// </summary>
    public record CommandGroupEntry(string GroupName, string ExtensionName, string Description);

    static class CommandGroupIndex
    {
        private static readonly Dictionary<string, CommandGroupEntry> _commandGroups = new(StringComparer.OrdinalIgnoreCase);

        static CommandGroupIndex()
        {
            _commandGroups["Group"] = new("Group", "Resources", "Manage Azure resource groups.");
            _commandGroups["Login"] = new("Login", "Login", "Manage Azure authentication.");
            _commandGroups["Extension"] = new("Extension", "Extension", "Manage CLI extensions.");
            // Add more command groups as needed
        }

        internal static string getExtensionNameByCommandGroupName(string commandGroupName)
        {
            if (!_commandGroups.TryGetValue(commandGroupName, out var entry))
            {
                throw new KeyNotFoundException($"Command group '{commandGroupName}' not found.");
            }
            return entry.ExtensionName;
        }

        /// <summary>
        /// Returns all registered command groups for help rendering.
        /// </summary>
        internal static IEnumerable<CommandGroupEntry> GetAllGroups() => _commandGroups.Values;
    }
}
