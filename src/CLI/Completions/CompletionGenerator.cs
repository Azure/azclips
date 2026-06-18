using System.Text.Json;
using Core.Abstractions;
using Core.Cli.Parser;
using Core.DependencyInjection;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli.Completions;

/// <summary>
/// Outputs a JSON completion tree built from ExtensionIndex metadata.
/// Used by shell tab-completion scripts (e.g. az.completions.ps1).
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
            var commandGroups = CommandGroupIndex.GetAllCommandGroups();

            // Build: { "group": { "_subcommands": ["create","list"], "create": ["--name","--location"], ... }, ... }
            var tree = new Dictionary<string, object>();

            foreach (var (groupName, extensionName) in commandGroups)
            {
                try
                {
                    var assembly = assemblyLoader.LoadExtension(extensionName);
                    var index = assembly.GetExtensionIndex();
                    var commandTable = index.GetCommandTable(ClientType.CLI);

                    var groupEntry = new Dictionary<string, object>();
                    var subcommands = new List<string>();

                    foreach (var (cliName, meta) in commandTable)
                    {
                        var paramNames = meta.Parameters.Keys.Select(k => $"--{k}").ToList();

                        if (cliName == "")
                        {
                            // Default command (e.g., "login" with no subcommand)
                            groupEntry["_params"] = paramNames;
                        }
                        else
                        {
                            subcommands.Add(cliName);
                            groupEntry[cliName] = paramNames;
                        }
                    }

                    groupEntry["_subcommands"] = subcommands;
                    tree[groupName.ToLowerInvariant()] = groupEntry;
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
