using System.Text.Json;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Extensions;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli.Completions;

/// <summary>
/// Outputs a JSON completion tree built from ExtensionIndex metadata.
/// Used by shell tab-completion scripts (e.g. azv2.completions.ps1).
/// Supports both full tree generation and context-aware discovery for specific command groups.
/// </summary>
internal static class CompletionGenerator
{
    /// <summary>
    /// Returns true and sets <paramref name="exitCode"/> when <c>--completions</c> is
    /// the first argument, so the caller can short-circuit.
    /// Usage: azv2 --completions [command-group] [subcommand...]
    /// Examples:
    ///   azv2 --completions              (full tree)
    ///   azv2 --completions vm           (commands in vm group)
    ///   azv2 --completions vm create    (specific command metadata)
    /// </summary>
    public static bool TryHandle(string[] args, ClientType clientType, out int exitCode)
    {
        if (args.Length > 0 && args[0] == "--completions")
        {
            // Extract any command context passed after --completions
            var commandContext = args.Length > 1 ? args.Skip(1).ToList() : new List<string>();
            exitCode = Generate(clientType, commandContext);
            return true;
        }
        exitCode = 0;
        return false;
    }

    private static int Generate(ClientType clientType, List<string>? commandContext = null)
    {
        commandContext ??= new List<string>();

        CirrusServiceProvider.PrependRegistration(services =>
        {
            services.AddSingleton<IExtensionLoader, BuiltinExtensionLoader>();
            services.AddSingleton<IExtensionLoader, MultiVersionExtensionLoader>();
        });
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
        });
        CirrusServiceProvider.Initialize(clientType);

        try
        {
            var assemblyLoader = CirrusServiceProvider.GetRequiredService<IAssemblyLoadingProvider>();
            var engine = CirrusServiceProvider.GetRequiredService<IEngine>();

            // Parse command context: first token is command group name (verb)
            string? commandGroupName = commandContext.Count > 0 ? commandContext[0].ToLowerInvariant() : null;
            string? subcommand = commandContext.Count > 1 ? commandContext[1].ToLowerInvariant() : null;

            // If a command group is present, do context-aware discovery
            if (!string.IsNullOrEmpty(commandGroupName))
            {
                return GenerateForCommandGroup(clientType, engine, assemblyLoader, commandGroupName, subcommand);
            }

            // Otherwise, generate full completion tree (existing behavior)
            return GenerateFullTree(clientType, engine, assemblyLoader);
        }
        finally
        {
            CirrusServiceProvider.Dispose();
        }
    }

    /// <summary>
    /// Generate completion data for a specific command group.
    /// This extracts the group's data from the full tree using discovery + assembly loading.
    /// Allows dynamic, context-aware completion for faster shell feedback.
    /// </summary>
    private static int GenerateForCommandGroup(ClientType clientType, IEngine engine, IAssemblyLoadingProvider assemblyLoader, string commandGroupName, string? subcommand)
    {
        var registryStore = new FileExtensionRegistryStore();
        var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.LoadReconciled());
        var packageIds = leafIndex.Entries.Values
            .Select(v => v.PackageId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build tree by querying only extensions that contain the requested command group
        var tree = new Dictionary<string, object>();

        foreach (var packageId in packageIds)
        {
            try
            {
                var extensionName = Constants.StripCirrusPrefix(packageId);
                var assembly = assemblyLoader.LoadExtension(extensionName);
                var index = assembly.GetExtensionIndex();
                var commandTable = index.GetCommandTable(ClientType.CLI);

                // Look for commands in this group across the command table
                foreach (var (cliName, meta) in commandTable)
                {
                    var tokens = cliName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0)
                        continue;

                    var verb = tokens[0].ToLowerInvariant();
                    
                    // Only include commands from the requested group
                    if (!verb.Equals(commandGroupName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var path = string.Join(' ', tokens.Skip(1));

                    var paramNames = meta.Parameters.Keys.Select(k => $"--{k}").ToList();
                    var requiredParams = meta.Parameters
                        .Where(p => p.Value.IsRequired)
                        .Select(p => $"--{p.Key}")
                        .ToList();

                    if (string.IsNullOrEmpty(path))
                    {
                        // Bare-verb command (e.g., "login")
                        tree["_params"] = paramNames;
                        if (requiredParams.Count > 0)
                        {
                            tree["_required"] = requiredParams;
                        }
                    }
                    else
                    {
                        // Subcommand
                        if (!tree.TryGetValue("_subcommands", out var subsObj) || subsObj is not List<string> subcommands)
                        {
                            subcommands = new List<string>();
                            tree["_subcommands"] = subcommands;
                        }

                        if (!subcommands.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            subcommands.Add(path);
                        }

                        tree[path] = paramNames;
                        if (requiredParams.Count > 0)
                        {
                            tree[$"{path}._req"] = requiredParams;
                        }
                    }
                }
            }
            catch
            {
                // Skip extensions that fail to load
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    /// <summary>
    /// Generate full completion tree for all available commands.
    /// This is called when no specific command group is provided.
    /// </summary>
    private static int GenerateFullTree(ClientType clientType, IEngine engine, IAssemblyLoadingProvider assemblyLoader)
    {
        var discoveredPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var registryStore = new FileExtensionRegistryStore();
        var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.LoadReconciled());
        var fallbackPackageIds = leafIndex.Entries.Values
            .Select(v => v.PackageId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discoveryExtensionNames = fallbackPackageIds
            .Select(Constants.StripCirrusPrefix)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discoveryStreams = new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                if (value is CommandMetadata metadata && !string.IsNullOrWhiteSpace(metadata.AssemblyName))
                {
                    discoveredPackageIds.Add(metadata.AssemblyName);
                }
                return Task.CompletedTask;
            },
            WriteError: (value, ct) => Task.CompletedTask,
            WriteVerbose: (value, ct) => Task.CompletedTask,
            WriteDebug: (value, ct) => Task.CompletedTask,
            WriteWarning: (value, ct) => Task.CompletedTask,
            WriteInformation: (value, ct) => Task.CompletedTask
        );

        using (var discoverySession = engine.OpenDiscoverySession(discoveryExtensionNames, commandName: null, discoveryStreams, isTransient: true))
        {
            discoverySession.RunAsync().GetAwaiter().GetResult();
        }

        // Discovery emits command metadata; if none are discovered, fall back to registry+seed.
        IEnumerable<string> packageIds = discoveredPackageIds;
        if (!packageIds.Any())
        {
            packageIds = fallbackPackageIds;
        }

        // Build: { "group": { "_subcommands": ["create","list"], "create": ["--name","--location"], ... }, ... }
        var tree = new Dictionary<string, object>();

        foreach (var packageId in packageIds)
        {
            try
            {
                var extensionName = Constants.StripCirrusPrefix(packageId);
                var assembly = assemblyLoader.LoadExtension(extensionName);
                var index = assembly.GetExtensionIndex();
                var commandTable = index.GetCommandTable(ClientType.CLI);

                foreach (var (cliName, meta) in commandTable)
                {
                    var tokens = cliName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0)
                    {
                        continue;
                    }

                    var verb = tokens[0].ToLowerInvariant();
                    var path = string.Join(' ', tokens.Skip(1));

                    if (!tree.TryGetValue(verb, out var existing) || existing is not Dictionary<string, object> groupEntry)
                    {
                        groupEntry = new Dictionary<string, object>();
                        tree[verb] = groupEntry;
                    }

                    if (!groupEntry.TryGetValue("_subcommands", out var subsObj) || subsObj is not List<string> subcommands)
                    {
                        subcommands = new List<string>();
                        groupEntry["_subcommands"] = subcommands;
                    }

                    var paramNames = meta.Parameters.Keys.Select(k => $"--{k}").ToList();
                    var requiredParams = meta.Parameters
                        .Where(p => p.Value.IsRequired)
                        .Select(p => $"--{p.Key}")
                        .ToList();

                    if (string.IsNullOrEmpty(path))
                    {
                        // Bare-verb command (e.g., "login")
                        groupEntry["_params"] = paramNames;
                        if (requiredParams.Count > 0)
                        {
                            groupEntry["_required"] = requiredParams;
                        }
                    }
                    else
                    {
                        if (!subcommands.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            subcommands.Add(path);
                        }
                        groupEntry[path] = paramNames;
                        if (requiredParams.Count > 0)
                        {
                            groupEntry[$"{path}._req"] = requiredParams;
                        }
                    }
                }
            }
            catch
            {
                // Skip extensions that fail to load
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }
}
