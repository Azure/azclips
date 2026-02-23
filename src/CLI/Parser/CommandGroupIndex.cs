// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Cli.Parser
{
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
    }
}
