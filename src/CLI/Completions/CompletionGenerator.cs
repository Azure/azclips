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
/// <remarks>
/// Raw <see cref="Console.WriteLine(string)"/> is intentional throughout this class:
/// the output is the shell-completion protocol payload consumed by completion scripts,
/// not diagnostic logging, so it must not be routed through <c>ILoggingService</c>
/// (which would prefix, level-gate, and stream-route it).
/// </remarks>
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

        if (!IsCompletionEnabled())
        {
            Console.WriteLine("{}");
            return 0;
        }

        // Extract any command context passed after --completions
        var commandContext = args.Length > 1 ? args.Skip(1).ToList() : new List<string>();
        return await GenerateAsync(clientType, commandContext);
    }

    /// <summary>
    /// Handles <c>--completion-candidates</c> when it is the first argument and returns the exit code.
    /// Returns <see langword="null"/> when candidate generation should not run.
    /// Outputs a space-separated list of completion candidates (plain text, no JSON)
    /// for the given command context. Used by bash/zsh completion scripts to avoid
    /// requiring a JSON parser (jq).
    /// Usage: az --completion-candidates [command-path...]
    /// The command path is walked through the command tree, so groups of any depth work.
    /// Each invocation returns the next tokens (intermediate groups / subcommands) that
    /// extend the path, or the parameter flags when the path is a complete command.
    /// Examples:
    ///   az --completion-candidates                        (top-level groups: vm storage deployment ...)
    ///   az --completion-candidates deployment             (next tokens: group)
    ///   az --completion-candidates deployment group       (subcommands: create what-if)
    ///   az --completion-candidates deployment group create (params: --name --template-file ...)
    /// </summary>
    public static async Task<int?> TryHandleCandidatesAsync(string[] args, ClientType clientType)
    {
        // @TODO (DanielMicrosoft) move flag to globals.
        if (args.Length == 0 || args[0] != "--completion-candidates")
        {
            return null;
        }

        if (!IsCompletionEnabled())
        {
            return 0;
        }

        var commandContext = args.Length > 1 ? args.Skip(1).ToList() : new List<string>();
        return await GenerateCandidatesAsync(clientType, commandContext);
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

    /// <summary>
    /// Generate completion data for a specific command group.
    /// This extracts the group's data from discovery output keyed by invocation name.
    /// Allows dynamic, context-aware completion for faster shell feedback.
    /// </summary>
    private static async Task<int> GenerateForCommandGroupAsync(ClientType clientType, IEngine engine, string commandGroupName, string? subcommand)
    {
        // Verb ownership requires discovery; filter output post-hoc.
        var discoveredCommands = await DiscoverCommandsAsync(clientType, engine, EnumerateExtensionNames());
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
        var discoveredCommands = await DiscoverCommandsAsync(clientType, engine, EnumerateExtensionNames());
        var tree = BuildCompletionTree(discoveredCommands, scopedToVerb: false);

        Console.WriteLine(JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    /// <summary>
    /// Generates a space-separated list of completion candidates for the given context.
    /// Reuses the same discovery and tree-building logic as <see cref="GenerateAsync"/>,
    /// but outputs plain text instead of JSON so bash/zsh scripts need no JSON parser.
    /// </summary>
    private static async Task<int> GenerateCandidatesAsync(ClientType clientType, List<string> commandContext)
    {
        var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();

        // The command path is the leading run of non-flag tokens. Flags (and their
        // values) never appear in the command tree, so anything from the first
        // "-"/"--" token onward is ignored when navigating.
        var pathContext = commandContext
            .TakeWhile(token => !token.StartsWith('-'))
            .Select(token => token.ToLowerInvariant())
            .ToList();

        // Discovery is loader-driven for every level: bundled + installed tiers surface uniformly.
        var extensionNames = EnumerateExtensionNames();

        if (pathContext.Count == 0)
        {
            // Level 0: top-level group names from full discovery.
            var allCommands = await DiscoverCommandsAsync(clientType, engine, extensionNames);
            var fullTree = BuildCompletionTree(allCommands, scopedToVerb: false);
            Console.WriteLine(string.Join(' ', fullTree.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)));
            return 0;
        }

        // Commands are keyed by full invocation name; ComputeCandidates walks the tree by prefix.
        var discoveredCommands = await DiscoverCommandsAsync(clientType, engine, extensionNames);
        var candidates = ComputeCandidates(discoveredCommands, pathContext);

        Console.WriteLine(string.Join(' ', candidates));
        return 0;
    }

    /// <summary>
    /// Walks the discovered command set relative to the typed command path and returns
    /// the completion candidates:
    /// <list type="bullet">
    ///   <item>the next path token for every command whose invocation name extends
    ///   <paramref name="pathContext"/> (intermediate groups and subcommands), and</item>
    ///   <item>the parameter flags of a command whose invocation name matches
    ///   <paramref name="pathContext"/> exactly (leaf command).</item>
    /// </list>
    /// Results are deduplicated (case-insensitive) and sorted for stable output.
    /// </summary>
    internal static List<string> ComputeCandidates(
        IEnumerable<KeyValuePair<string, CommandMetadata>> commands,
        IReadOnlyList<string> pathContext)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (invocationName, meta) in commands)
        {
            var tokens = invocationName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!IsPrefix(pathContext, tokens))
            {
                continue;
            }

            if (tokens.Length > pathContext.Count)
            {
                // Intermediate group or subcommand: offer the next path token.
                var next = tokens[pathContext.Count];
                if (seen.Add(next))
                {
                    candidates.Add(next);
                }
            }
            else
            {
                // Exact command match: offer its parameter flags.
                foreach (var parameterName in meta.Parameters.Keys)
                {
                    var flag = $"--{parameterName}";
                    if (seen.Add(flag))
                    {
                        candidates.Add(flag);
                    }
                }
            }
        }

        candidates.Sort(StringComparer.OrdinalIgnoreCase);
        return candidates;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="prefix"/> is a leading
    /// subsequence of <paramref name="tokens"/> (case-insensitive).
    /// </summary>
    private static bool IsPrefix(IReadOnlyList<string> prefix, string[] tokens)
    {
        if (prefix.Count > tokens.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Count; i++)
        {
            if (!tokens[i].Equals(prefix[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns one name per package, using the copies routing selected so completions offer the
    /// commands dispatch would run. Falls back to every on-disk name when routing selected nothing.
    /// </summary>
    private static IList<string> EnumerateExtensionNames()
    {
        try
        {
            var selected = CliExtensionServiceRegistration.GetRoutingSelectedPackageIds(
                AzclipsServiceProvider.GetRequiredService<Parser.ExtensionDispatcher>(),
                AzclipsServiceProvider.GetRequiredService<SelectedExtensionLocations>());
            if (selected.Count > 0)
            {
                return selected.ToList();
            }
        }
        catch (Exception)
        {
            // Completion must stay usable; fall back to the loaders' on-disk names.
        }

        return EnumerateOnDiskExtensionNames();
    }

    /// <summary>
    /// Returns short names of every extension the loader chain reports on disk (bundled + installed
    /// tiers combined). Delegates to <see cref="IAssemblyLoadingProvider.GetAllInstalledExtensions"/>
    /// so the loaders own search-path knowledge; completion no longer duplicates their disk-walk.
    /// </summary>
    private static IList<string> EnumerateOnDiskExtensionNames()
    {
        var provider = AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingProvider>();
        var installed = provider.GetAllInstalledExtensions();
        if (installed is null)
        {
            return [];
        }

        return installed
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
