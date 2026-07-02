using System;
using System.Collections.Generic;

namespace Core.Tui.Services;

/// <summary>
/// Static command tree for AzClips CLI autocomplete.
/// Mirrors the command groups, subcommands, and arguments registered in the extension system.
/// </summary>
static class CommandTree
{
    public record ArgInfo(string Name, string Description, bool Required);

    public record CommandInfo(string Name, string Description, ArgInfo[] Args);

    public record GroupInfo(string Name, string Description, CommandInfo[] Commands);

    public static readonly GroupInfo[] Groups =
    [
        new("group", "Manage resource groups", [
            new("list", "List all resource groups", [
                new("--subscription-id", "Azure Subscription Id", false),
            ]),
            new("create", "Create a resource group", [
                new("--subscription-id", "Azure Subscription Id", true),
                new("--name", "The name of the resource group", true),
                new("--resource-id", "The resource id of the resource group", false),
                new("--location", "Location for resource group", true),
            ]),
            new("get", "Get a resource group", [
                new("--subscription-id", "Azure Subscription Id", false),
                new("--name", "The name of the resource group", false),
            ]),
        ]),
        new("login", "Manage authentication", [
            new("", "Login to Azure (default)", [
                new("--subscription-id", "Azure Subscription Id", true),
                new("--tenant-id", "Tenant ID to login to", false),
                new("--use-device-code", "Use device code flow for authentication", false),
            ]),
            new("logout", "Logout from Azure", []),
            new("show", "Show the active profile", []), // todo: outdated
        ]),
        new("extension", "Manage extensions", [
            new("add", "Add an extension", [
                new("--name", "Extension name", false),
                new("--version", "Extension version", false),
                new("--feed-url", "NuGet feed URL", false),
                new("--path", "Local path to extension", false),
            ]),
        ]),
        // VFS navigation commands (handled locally, not forwarded to CLI)
        new("cd", "Navigate Azure VFS path", [
            new("", "Change directory (cd /tenant/sub/rg)", []),
        ]),
        new("ls", "List Azure VFS children", [
            new("", "List items at path (ls [path])", []),
        ]),
        new("pwd", "Show current VFS path", [
            new("", "Print working directory and ARM resource ID", []),
        ]),
        new("tree", "Show Azure VFS tree", [
            new("", "Display resource hierarchy (tree [depth])", []),
        ]),
        new("refresh", "Refresh Azure VFS cache", [
            new("", "Refresh caches (refresh [all|tenants|subs|rgs|resources])", []),
        ]),
        new("mode", "Switch between Azure and Local mode", [
            new("", "Toggle navigation mode (F2)", []),
        ]),
    ];

    /// <summary>
    /// Returns autocomplete suggestions based on current input tokens.
    /// </summary>
    public static List<Suggestion> GetSuggestions(string input)
    {
        var results = new List<Suggestion>();
        var trimmed = input.TrimStart();
        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Level 0: no tokens or typing a group name
        if (tokens.Length == 0 || (tokens.Length == 1 && !trimmed.EndsWith(' ')))
        {
            var prefix = tokens.Length == 1 ? tokens[0] : "";
            foreach (var g in Groups)
            {
                if (g.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    results.Add(new Suggestion(g.Name, g.Description));
            }
            return results;
        }

        // Find matching group
        var group = Array.Find(Groups, g => g.Name.Equals(tokens[0], StringComparison.OrdinalIgnoreCase));
        if (group == null) return results;

        // Check if this group has a default (unnamed) command
        var defaultCmd = Array.Find(group.Commands, c => c.Name == "");

        // Level 1: group typed, suggesting subcommand (+ default command args)
        if (tokens.Length == 1 || (tokens.Length == 2 && !trimmed.EndsWith(' ')))
        {
            var prefix = tokens.Length >= 2 ? tokens[1] : "";
            foreach (var cmd in group.Commands)
            {
                if (string.IsNullOrEmpty(cmd.Name)) continue;
                if (cmd.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    results.Add(new Suggestion(cmd.Name, cmd.Description));
            }
            // Also suggest default command's args (e.g. "login --subscription-id")
            if (defaultCmd != null)
            {
                foreach (var arg in defaultCmd.Args)
                {
                    if (arg.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var req = arg.Required ? " (required)" : "";
                        results.Add(new Suggestion(arg.Name, $"{arg.Description}{req}"));
                    }
                }
            }
            return results;
        }

        // Determine which command is being used
        var command = Array.Find(group.Commands, c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals(tokens[1], StringComparison.OrdinalIgnoreCase));
        int argStartIndex;
        if (command != null)
        {
            // Named subcommand matched (e.g. "login logout", "group list")
            argStartIndex = 2;
        }
        else if (defaultCmd != null)
        {
            // No subcommand match — fall back to default command (e.g. "login --subscription-id ...")
            command = defaultCmd;
            argStartIndex = 1; // args start right after the group name
        }
        else
        {
            return results;
        }

        // Find which args are already used
        var usedArgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = argStartIndex; i < tokens.Length; i++)
        {
            if (tokens[i].StartsWith("--"))
                usedArgs.Add(tokens[i]);
        }

        // The last token being typed
        var lastToken = trimmed.EndsWith(' ') ? "" : tokens[^1];
        var isTypingArg = lastToken.StartsWith("-");

        if (trimmed.EndsWith(' ') || isTypingArg)
        {
            var prefix = isTypingArg ? lastToken : "";
            foreach (var arg in command.Args)
            {
                if (usedArgs.Contains(arg.Name)) continue;
                if (arg.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var req = arg.Required ? " (required)" : "";
                    results.Add(new Suggestion(arg.Name, $"{arg.Description}{req}"));
                }
            }
        }

        return results;
    }
}
