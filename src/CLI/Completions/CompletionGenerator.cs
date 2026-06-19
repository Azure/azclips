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
/// </summary>
internal static class CompletionGenerator
{
    /// <summary>
    /// Returns true and sets <paramref name="exitCode"/> when <c>--completions</c> is
    /// the first argument, so the caller can short-circuit.
    /// </summary>
    public static bool TryHandle(string[] args, ClientType clientType, out int exitCode)
    {
        if (args.Length > 0 && args[0] == "--completions")
        {
            exitCode = Generate(clientType);
            return true;
        }
        exitCode = 0;
        return false;
    }

    private static int Generate(ClientType clientType)
    {
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
            var registryStore = new FileExtensionRegistryStore();
            var leafIndex = LeafIndex.Build(BuiltInExtensions.Map, registryStore.LoadReconciled());
            var packageIds = leafIndex.Entries.Values
                .Select(v => v.PackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase);

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
        finally
        {
            CirrusServiceProvider.Dispose();
        }
    }
}
