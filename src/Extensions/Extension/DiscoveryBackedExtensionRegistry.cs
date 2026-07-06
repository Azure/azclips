using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

internal static class DiscoveryBackedExtensionRegistry
{
    private static readonly HashSet<string> s_builtInPackageIds = BuiltInExtensions.Map.Values
        .Select(static value => value.PackageId)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static ExtensionRegistry BuildDiscoveredWithPreferences(ExtensionRegistry fileRegistry)
    {
        ArgumentNullException.ThrowIfNull(fileRegistry);

        ILoggingService? loggingService = null;
        try
        {
            loggingService = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
        }
        catch
        {
            // Registry synthesis should not fail if logging service is unavailable.
        }

        var discovered = new ExtensionRegistry { SchemaVersion = ExtensionRegistry.CurrentSchemaVersion };
        var infos = RunDiscoverySession();

        foreach (var group in infos.GroupBy(static info => info.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            fileRegistry.Extensions.TryGetValue(group.Key, out var fileEntry);
            var selection = ExtensionCandidateSelector.Select(group.ToList(), fileEntry);
            loggingService?.Debug(ExtensionCandidateSelector.BuildDiagnostics(
                context: "DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences",
                selection: selection,
                persistedPreference: fileEntry));
            var info = selection.Selected;

            if (s_builtInPackageIds.Contains(info.PackageId))
            {
                continue;
            }

            var entry = new ExtensionRegistryEntry
            {
                Verb = info.Verbs.FirstOrDefault(),
                Version = info.ResolvedVersion,
                Pinned = false,
                Source = null,
                InstalledAt = DateTimeOffset.UtcNow,
            };

            foreach (var ns in info.Verbs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                entry.Namespaces.Add(ns);
            }

            foreach (var command in info.Commands.Keys)
            {
                var (verb, path) = ToContribution(command);
                if (string.IsNullOrEmpty(verb))
                {
                    continue;
                }

                if (!entry.Contributions.Any(c =>
                    string.Equals(c.Verb, verb, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    entry.Contributions.Add(new Contribution { Verb = verb, Path = path });
                }
            }

            // Some extension indexes may not populate Verbs; derive from discovered command leaves.
            if (entry.Namespaces.Count == 0)
            {
                foreach (var derivedNs in entry.Contributions
                    .Select(static c => c.Verb)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    entry.Namespaces.Add(derivedNs);
                }
            }

            if (string.IsNullOrWhiteSpace(entry.Verb))
            {
                entry.Verb = entry.Namespaces.FirstOrDefault();
            }

            discovered.Extensions[info.PackageId] = entry;
        }

        foreach (var (packageId, fileEntry) in fileRegistry.Extensions)
        {
            if (!discovered.Extensions.TryGetValue(packageId, out var discoveredEntry))
            {
                continue;
            }

            discoveredEntry.Pinned = fileEntry.Pinned;
            discoveredEntry.Source = fileEntry.Source;
            discoveredEntry.InstalledAt = fileEntry.InstalledAt;
        }

        return discovered;
    }

    private static List<DiscoveredExtensionInfo> RunDiscoverySession()
    {
        var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        var discoveredInfos = new List<DiscoveredExtensionInfo>();

        var outputStreams = new OutputStreams(
            WriteOutput: async (result, _) =>
            {
                if (result is List<DiscoveredExtensionInfo> infos)
                {
                    discoveredInfos.AddRange(infos);
                }

                await Task.CompletedTask;
            },
            WriteVerbose: async (_, _) => { await Task.CompletedTask; },
            WriteDebug: async (_, _) => { await Task.CompletedTask; },
            WriteWarning: async (_, _) => { await Task.CompletedTask; },
            WriteError: async (_, _) => { await Task.CompletedTask; },
            WriteInformation: async (_, _) => { await Task.CompletedTask; });

        using var session = engine.OpenDiscoverySession(
            extensionNames: [],
            commandName: null,
            outputStreams: outputStreams,
            isTransient: true);

        session.RunAsync().GetAwaiter().GetResult();
        return discoveredInfos;
    }

    private static (string Verb, string Path) ToContribution(string invocation)
    {
        if (string.IsNullOrWhiteSpace(invocation))
        {
            return (string.Empty, string.Empty);
        }

        var parts = invocation.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var verb = parts[0];
        var path = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
        return (verb, path);
    }
}
