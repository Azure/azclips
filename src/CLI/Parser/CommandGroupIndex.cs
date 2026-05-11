// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
namespace Core.Cli.Parser
{
    //This index will eventually comes from:
    //1. Core modules: Login, Extension, etc; Which will be predefined with Az
    //2. Extensions: Resources, Compute, etc; Which will be registered on installation
    //3. Fallback to runtime calculation based on command name, which will be expensive but should be rare
    static class CommandGroupIndex
    {
        private static readonly Dictionary<string, string> _commandGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static CommandGroupIndex()
        {
            _commandGroups["Group"] = "Resources";
            _commandGroups["Login"] = "Login";
            _commandGroups["Extension"] = "Extension";
            _commandGroups["Config"] = "Config";
            // Add more command groups as needed
        }

        internal static string getExtensionNameByCommandGroupName(string commandGroupName)
        {
            if (!_commandGroups.TryGetValue(commandGroupName, out var extensionName))
            {
                throw new KeyNotFoundException($"Command group '{commandGroupName}' not found.");
            }
            return extensionName;
        }

        internal static string GetCommandGroupNameByExtensionName(string extensionName)
        {
            foreach (var kvp in _commandGroups)
            {
                if (kvp.Value.Equals(extensionName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            throw new KeyNotFoundException($"Extension '{extensionName}' does not have a registered command group.");
        }

        internal static IReadOnlyList<string> GetRegisteredExtensionNames()
            => [.. _commandGroups.Values.Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
