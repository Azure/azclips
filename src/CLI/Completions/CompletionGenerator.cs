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
    /// <summary>
    /// Handles <c>--completions</c> when it is the first argument and returns the exit code.
    /// Returns <see langword="null"/> when completion generation should not run.
    /// Usage: az --completions [command-group] [subcommand...]
    /// Examples:
    ///   az --completions              (full tree)
    ///   az --completions vm           (commands in vm group)
    ///   az --completions vm create    (specific command metadata)
    /// </summary>
    public static async Task<int?> TryHandleAsync(string[] args, ClientType clientType)
    {
        if (args.Length == 0 || args[0] != "--completions")
        {
            return null;
        }

        // Extract any command context passed after --completions
        var commandContext = args.Length > 1 ? args.Skip(1).ToList() : new List<string>();
        return await GenerateAsync(clientType, commandContext);
    }

    private static async Task<int> GenerateAsync(ClientType clientType, List<string>? commandContext = null)
    {
        commandContext ??= new List<string>();

        var engine = CirrusServiceProvider.GetRequiredService<IEngine>();

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

    /// <summary>
    /// Generate completion data for a specific command group.
    /// This extracts the group's data from discovery output keyed by invocation name.
    /// Allows dynamic, context-aware completion for faster shell feedback.
    /// </summary>
    private static async Task<int> GenerateForCommandGroupAsync(ClientType clientType, IEngine engine, string commandGroupName, string? subcommand)
    {
        var registryStore = new FileExtensionRegistryStore();
        var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.LoadReconciled());
        var discoveryExtensionNames = leafIndex.Entries.Values
            .Where(v => v.Verb.Equals(commandGroupName, StringComparison.OrdinalIgnoreCase))
            .Select(v => Constants.StripCirrusPrefix(v.PackageId))
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
        var registryStore = new FileExtensionRegistryStore();
        var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.LoadReconciled());
        var discoveryExtensionNames = leafIndex.Entries.Values
            .Select(v => Constants.StripCirrusPrefix(v.PackageId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var discoveredCommands = await DiscoverCommandsAsync(clientType, engine, discoveryExtensionNames);
        var tree = BuildCompletionTree(discoveredCommands, scopedToVerb: false);

        Console.WriteLine(JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    private static async Task<Dictionary<string, CommandMetadata>> DiscoverCommandsAsync(ClientType clientType, IEngine engine, IList<string> extensionNames)
    {
        var discoveredCommands = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase);

        var discoveryStreams = new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                if (value is IDictionary<string, CommandMetadata> discovered)
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
