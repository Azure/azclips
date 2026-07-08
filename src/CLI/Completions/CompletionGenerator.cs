// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Extensions;
using Core.Runtime;

namespace Core.Cli.Completions;

/// <summary>
/// Outputs a JSON completion tree built from ExtensionIndex metadata.
/// Used by shell tab-completion scripts (e.g. az.completions.ps1).
/// Supports both full tree generation and context-aware discovery for specific command groups.
/// </summary>
internal static class CompletionGenerator
{
    private const string JsonCompletionsCommand = "--completions";
    private const string CandidateCompletionsCommand = "--completion-candidates";

    /// <summary>
    /// Handles <c>--completions</c> and <c>--completion-candidates</c> when either is the first argument and returns the exit code.
    /// Returns <see langword="null"/> when completion generation should not run.
    /// Usage: az --completions [command-group] [subcommand...]
    ///        az --completion-candidates [command-group] [subcommand...]
    /// Examples:
    ///   az --completions              (full tree)
    ///   az --completions vm           (commands in vm group)
    ///   az --completions vm create    (specific command metadata)
    ///   az --completion-candidates    (top-level groups, one per line)
    ///   az --completion-candidates vm (subcommands and group params, one per line)
    /// </summary>
    public static async Task<int?> TryHandleAsync(string[] args, ClientType clientType)
    {
        if (args.Length == 0 || (args[0] != JsonCompletionsCommand && args[0] != CandidateCompletionsCommand))
        {
            return null;
        }

        var isCandidateMode = args[0] == CandidateCompletionsCommand;
        if (!IsCompletionEnabled())
        {
            if (!isCandidateMode)
            {
                Console.WriteLine("{}");
            }
            return 0;
        }

        // Extract any command context passed after the completion command.
        var commandContext = args.Length > 1 ? args.Skip(1).ToList() : new List<string>();
        if (isCandidateMode)
        {
            return await GenerateCandidatesAsync(clientType, commandContext);
        }

        return await GenerateAsync(clientType, commandContext);
    }

    /// <summary>
    /// Resolves the configuration service and reports whether shell tab-completion
    /// is enabled. Isolated so the DI lookup can fail independently of the config read.
    /// </summary>
    private static bool IsCompletionEnabled()
    {
        try
        {
            var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            return ResolveEnabled(config);
        }
        catch
        {
            // Configuration service unavailable
            return true;
        }
    }

    internal static bool ResolveEnabled(IConfigurationService config)
    {
        try
        {
            return config.GetConfiguration<CompletionOptions>().Enabled;
        }
        catch
        {
            return true;
        }
    }

    private static async Task<int> GenerateAsync(ClientType clientType, List<string>? commandContext = null)
    {
        commandContext ??= new List<string>();

        var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();

        // Parse command context: first token is command group name (verb)
        string? commandGroupName = commandContext.Count > 0 ? commandContext[0].ToLowerInvariant() : null;
        string? subcommand = commandContext.Count > 1 ? commandContext[1].ToLowerInvariant() : null;

        // If a command group is present, do context-aware discovery
        if (!string.IsNullOrEmpty(commandGroupName))
        {
            return await GenerateForCommandGroupAsync(clientType, engine, commandGroupName, subcommand);
        }

        // Otherwise, generate full completion tree (existing behavior)
        return await GenerateFullTreeAsync(clientType, engine);
    }

    private static async Task<int> GenerateCandidatesAsync(ClientType clientType, List<string>? commandContext = null)
    {
        commandContext ??= new List<string>();

        var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        var commandGroupName = commandContext.Count > 0 ? commandContext[0].ToLowerInvariant() : null;
        var subcommand = commandContext.Count > 1 ? commandContext[1].ToLowerInvariant() : null;

        var discoveredCommands = string.IsNullOrEmpty(commandGroupName)
            ? await DiscoverFullTreeCommandsAsync(clientType, engine)
            : await DiscoverCommandGroupCommandsAsync(clientType, engine, commandGroupName, subcommand: null);

        foreach (var candidate in BuildCandidateList(discoveredCommands, commandGroupName, subcommand))
        {
            Console.WriteLine(candidate);
        }

        return 0;
    }

    /// <summary>
    /// Generate completion data for a specific command group.
    /// This extracts the group's data from discovery output keyed by invocation name.
    /// Allows dynamic, context-aware completion for faster shell feedback.
    /// </summary>
    private static async Task<int> GenerateForCommandGroupAsync(ClientType clientType, IEngine engine, string commandGroupName, string? subcommand)
    {
        var filteredCommands = await DiscoverCommandGroupCommandsAsync(clientType, engine, commandGroupName, subcommand);
        var tree = BuildCompletionTree(filteredCommands, scopedToVerb: true);

        Console.WriteLine(JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    /// <summary>
    /// Generate full completion tree for all available commands.
    /// This is called when no specific command group is provided.
    /// </summary>
    private static async Task<int> GenerateFullTreeAsync(ClientType clientType, IEngine engine)
    {
        var discoveredCommands = await DiscoverFullTreeCommandsAsync(clientType, engine);
        var tree = BuildCompletionTree(discoveredCommands, scopedToVerb: false);

        Console.WriteLine(JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    private static async Task<Dictionary<string, CommandMetadata>> DiscoverFullTreeCommandsAsync(ClientType clientType, IEngine engine)
    {
        var registryStore = new FileExtensionRegistryStore();
        var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.Load());
        var discoveryExtensionNames = leafIndex.Entries.Values
            .Select(v => Constants.StripKnownExtensionPrefix(v.PackageId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Also discover extensions available on disk (same roots the BuiltinExtensionLoader scans)
        foreach (var name in ScanLocalExtensionNames())
        {
            if (!discoveryExtensionNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                discoveryExtensionNames.Add(name);
            }
        }

        return await DiscoverCommandsAsync(clientType, engine, discoveryExtensionNames);
    }

    private static async Task<IEnumerable<KeyValuePair<string, CommandMetadata>>> DiscoverCommandGroupCommandsAsync(
        ClientType clientType,
        IEngine engine,
        string commandGroupName,
        string? subcommand)
    {
        var registryStore = new FileExtensionRegistryStore();
        var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.Load());
        var discoveryExtensionNames = leafIndex.Entries.Values
            .Where(v => v.Verb.Equals(commandGroupName, StringComparison.OrdinalIgnoreCase))
            .Select(v => Constants.StripKnownExtensionPrefix(v.PackageId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discoveredCommands = await DiscoverCommandsAsync(clientType, engine, discoveryExtensionNames);
        var filteredCommands = discoveredCommands
            .Where(kvp => kvp.Key.Equals(commandGroupName, StringComparison.OrdinalIgnoreCase)
                || kvp.Key.StartsWith($"{commandGroupName} ", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(subcommand))
        {
            var requestedKey = $"{commandGroupName} {subcommand}";
            filteredCommands = filteredCommands.Where(kvp => kvp.Key.Equals(requestedKey, StringComparison.OrdinalIgnoreCase));
        }

        return filteredCommands;
    }

    /// <summary>
    /// Scans the same candidate roots used by <c>BuiltinExtensionLoader</c> for directories
    /// matching the extension naming convention and returns short extension names (e.g. "Resources").
    /// </summary>
    private static IEnumerable<string> ScanLocalExtensionNames()
    {
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Extensions"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Debug")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Release")),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var dirName = Path.GetFileName(dir);
                var matchedPrefix = Constants.extensionPackagePrefixes
                    .FirstOrDefault(prefix => dirName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(matchedPrefix))
                {
                    var shortName = dirName[matchedPrefix.Length..];
                    if (!string.IsNullOrEmpty(shortName) && seen.Add(shortName))
                    {
                        yield return shortName;
                    }
                }
            }
        }
    }

    private static async Task<Dictionary<string, CommandMetadata>> DiscoverCommandsAsync(ClientType clientType, IEngine engine, IList<string> extensionNames)
    {
        var discoveredCommands = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase);

        var discoveryStreams = new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                if (value is IList<DiscoveredExtensionInfo> infos)
                {
                    foreach (var info in infos)
                    {
                        foreach (var (invocationName, metadata) in info.Commands)
                        {
                            discoveredCommands[invocationName] = metadata;
                        }
                    }
                }
                else if (value is IDictionary<string, CommandMetadata> discovered)
                {
                    foreach (var (invocationName, metadata) in discovered)
                    {
                        discoveredCommands[invocationName] = metadata;
                    }
                }
                return Task.CompletedTask;
            },
            WriteError: (value, ct) => Task.CompletedTask,
            WriteVerbose: (value, ct) => Task.CompletedTask,
            WriteDebug: (value, ct) => Task.CompletedTask,
            WriteWarning: (value, ct) => Task.CompletedTask,
            WriteInformation: (value, ct) => Task.CompletedTask
        );

        using var discoverySession = engine.OpenDiscoverySession(extensionNames, commandName: null, discoveryStreams, isTransient: true);
        await discoverySession.RunAsync();

        return discoveredCommands;
    }

    private static Dictionary<string, object> BuildCompletionTree(IEnumerable<KeyValuePair<string, CommandMetadata>> commands, bool scopedToVerb)
    {
        var tree = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var (invocationName, meta) in commands)
        {
            var tokens = invocationName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            var verb = tokens[0].ToLowerInvariant();
            var path = string.Join(' ', tokens.Skip(1));
            var groupEntry = scopedToVerb
                ? tree
                : GetOrCreateGroupEntry(tree, verb);

            var paramNames = meta.Parameters.Keys.Select(k => $"--{k}").ToList();
            var requiredParams = meta.Parameters
                .Where(p => p.Value.IsRequired)
                .Select(p => $"--{p.Key}")
                .ToList();
            var allowedValues = meta.Parameters
                .Where(p => p.Value.AllowedValues.Length > 0)
                .ToDictionary(p => $"--{p.Key}", p => (object)p.Value.AllowedValues);

            if (string.IsNullOrEmpty(path))
            {
                groupEntry["_params"] = paramNames;
                if (requiredParams.Count > 0)
                {
                    groupEntry["_required"] = requiredParams;
                }
                if (allowedValues.Count > 0)
                {
                    groupEntry["_values"] = allowedValues;
                }
                continue;
            }

            if (!groupEntry.TryGetValue("_subcommands", out var subsObj) || subsObj is not List<string> subcommands)
            {
                subcommands = new List<string>();
                groupEntry["_subcommands"] = subcommands;
            }

            if (!subcommands.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                subcommands.Add(path);
            }

            groupEntry[path] = paramNames;
            if (requiredParams.Count > 0)
            {
                groupEntry[$"{path}._req"] = requiredParams;
            }
            if (allowedValues.Count > 0)
            {
                groupEntry[$"{path}._values"] = allowedValues;
            }
        }

        return tree;
    }

    internal static List<string> BuildCandidateList(
        IEnumerable<KeyValuePair<string, CommandMetadata>> commands,
        string? commandGroupName,
        string? subcommand)
    {
        var commandList = commands.ToList();
        if (string.IsNullOrEmpty(commandGroupName))
        {
            return commandList
                .Select(kvp => kvp.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var groupCommand = commandList.FirstOrDefault(kvp => kvp.Key.Equals(commandGroupName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(subcommand))
        {
            var candidates = commandList
                .Select(kvp => kvp.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(tokens => tokens.Length > 1 && tokens[0].Equals(commandGroupName, StringComparison.OrdinalIgnoreCase))
                .Select(tokens => string.Join(' ', tokens.Skip(1)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrEmpty(groupCommand.Key))
            {
                candidates.AddRange(groupCommand.Value.Parameters.Keys.Select(k => $"--{k}"));
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        var requestedCommand = commandList.FirstOrDefault(kvp =>
            kvp.Key.Equals($"{commandGroupName} {subcommand}", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(requestedCommand.Key))
        {
            return requestedCommand.Value.Parameters.Keys.Select(k => $"--{k}").ToList();
        }

        if (!string.IsNullOrEmpty(groupCommand.Key))
        {
            return groupCommand.Value.Parameters.Keys.Select(k => $"--{k}").ToList();
        }

        return new List<string>();
    }

    private static Dictionary<string, object> GetOrCreateGroupEntry(Dictionary<string, object> tree, string verb)
    {
        if (!tree.TryGetValue(verb, out var existing) || existing is not Dictionary<string, object> groupEntry)
        {
            groupEntry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            tree[verb] = groupEntry;
        }

        return groupEntry;
    }
}
