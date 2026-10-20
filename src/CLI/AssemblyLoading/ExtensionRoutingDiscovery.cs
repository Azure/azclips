// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using Core.Abstractions;
using Core.Cli.Output;
using Core.Extensions;
using Core.Runtime;

namespace Core.Cli;

internal sealed class ExtensionRoutingDiscovery(IAssemblyLoadingService assemblyLoadingService, ILoggingService loggingService)
{
    public IReadOnlyList<ExtensionRoutingInfo> Discover()
    {
        var total = Stopwatch.StartNew();
        loggingService.Debug("[ExtensionRoutingDiscovery] Enumerating installed extensions.");
        var installations = assemblyLoadingService.LoadAllExtensions() ?? [];
        loggingService.Debug($"[ExtensionRoutingDiscovery] Enumerated {installations.Count} installations in {total.Elapsed.TotalMilliseconds:F1}ms.");
        var results = new List<ExtensionRoutingInfo>();
        var rows = new List<string[]>();
        var sidecars = 0;
        var fallbacks = 0;
        var failures = 0;

        foreach (var (info, load) in installations)
        {
            var timer = Stopwatch.StartNew();
            var source = "Sidecar";
            var sidecarPath = info.Location is null ? "(unknown)" : Path.ChangeExtension(info.Location, ".commands.json");
            loggingService.Debug($"[ExtensionRoutingDiscovery] Reading '{info.Name}' version='{info.Version}' sidecar='{sidecarPath}'.");
            if (ExtensionSidecarReader.TryRead(info, out var routing, out var reason))
            {
                results.Add(routing);
                sidecars++;
                loggingService.Debug($"[ExtensionRoutingDiscovery] Validated '{routing.PackageId}': {routing.Verbs.Count} verbs, {routing.Leaves.Count} leaves.");
            }
            else
            {
                source = "DLL fallback";
                fallbacks++;
                loggingService.Debug($"[ExtensionRoutingDiscovery] DLL fallback for '{info.Name}' version='{info.Version}': {reason}");
                try
                {
                    System.Reflection.Assembly assembly;
                    try
                    {
                        assembly = load();
                    }
                    catch (InvalidOperationException ex) when (
                        info.Installation is null &&
                        !string.IsNullOrWhiteSpace(info.Version) &&
                        ex.Message.Contains("could not be found by any extension loader", StringComparison.OrdinalIgnoreCase))
                    {
                        assembly = assemblyLoadingService.LoadExtension(info.Name, string.Empty);
                    }
                    var index = assembly.GetExtensionIndex();
                    var commands = index.GetAllCommands(ClientType.CLI).ToArray();
                    var packageId = commands.FirstOrDefault()?.AssemblyName
                        ?? index.GetType().Assembly.GetName().Name
                        ?? info.Name;
                    results.Add(new ExtensionRoutingInfo(
                        packageId, info.Version, ExtensionVerbDeclarations.GetDeclaredVerbs(index),
                        commands.Select(command => command.InvocationName).ToArray(), info.Location));
                }
                catch (Exception ex)
                {
                    // Match full discovery's warn-and-skip policy, without losing other installations.
                    failures++;
                    source = "Failed";
                    loggingService.Warn($"[ExtensionRoutingDiscovery] Failed to discover extension '{info.Name}': {ex.Message}");
                }
            }

            rows.Add([info.Name, source, timer.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(info.Version) ? "-" : info.Version]);
        }

        loggingService.Debug("[ExtensionRoutingDiscovery] Routing discovery timing:");
        foreach (var line in TableFormatter.FormatRows(rows, ["Extension", "Source", "Time (ms)", "Version"],
                     includeHeaderSeparator: false).Split('\n'))
        {
            loggingService.Debug($"[ExtensionRoutingDiscovery] {line}");
        }
        loggingService.Debug($"[ExtensionRoutingDiscovery] Discovery complete: sidecars={sidecars}, DLL fallbacks={fallbacks}, failures={failures}, total={total.Elapsed.TotalMilliseconds:F1}ms.");
        return results;
    }
}
