// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Loader;

namespace Azclips.Help.Tool;

/// <summary>
/// Shared build-time assembly-loading helpers used by <see cref="Program"/> and by the
/// sibling <c>Azclips.Cli.Export</c> tool (via <c>InternalsVisibleTo</c>).
/// </summary>
/// <remarks>
/// This is the tooling-side loader that reflects over just-built extension DLLs in
/// <c>artifacts/&lt;Configuration&gt;/</c>. It has no relationship to the runtime CLI's
/// on-demand extension loader in <c>src/CLI/AssemblyLoading/</c> and must not be
/// referenced from runtime code.
/// </remarks>
internal static class ExtensionAssemblyLoader
{
    /// <summary>
    /// Assembly-name prefixes we recognise as azclips extensions when enumerating DLLs
    /// under an artifacts root.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownExtensionPrefixes =
    [
        "Microsoft.Azure.Azclips.Extensions.",
    ];

    /// <summary>
    /// Installs an <see cref="AssemblyLoadContext.Default"/> resolver that satisfies
    /// missing dependencies by searching <paramref name="artifactsRoot"/> recursively.
    /// Idempotent-per-process is not attempted — call once at startup.
    /// </summary>
    public static void InstallResolver(string artifactsRoot)
    {
        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
        {
            var alreadyLoaded = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded is not null)
            {
                return alreadyLoaded;
            }

            var candidate = Directory
                .EnumerateFiles(artifactsRoot, assemblyName.Name + ".dll", SearchOption.AllDirectories)
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path.Contains($"{Path.DirectorySeparatorChar}Benchmark{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(static path => path.Length)
                .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return candidate == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(candidate));
        };
    }

    /// <summary>
    /// Enumerates extension assembly paths under <paramref name="artifactsRoot"/> matching
    /// <see cref="KnownExtensionPrefixes"/>, de-duplicated by
    /// <see cref="SelectPreferredAssemblyPaths"/>.
    /// </summary>
    public static List<string> EnumerateExtensionAssemblies(string artifactsRoot)
    {
        return SelectPreferredAssemblyPaths(KnownExtensionPrefixes
            .SelectMany(prefix => Directory.EnumerateFiles(artifactsRoot, $"{prefix}*.dll", SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Enumerates assembly paths under <paramref name="artifactsRoot"/> matching any of
    /// <paramref name="patterns"/> (glob-style, e.g. <c>"Microsoft.Azure.Azclips.Cmdlets.*.dll"</c>),
    /// de-duplicated by <see cref="SelectPreferredAssemblyPaths"/>.
    /// </summary>
    public static List<string> EnumerateAssembliesByPattern(string artifactsRoot, IEnumerable<string> patterns)
    {
        return SelectPreferredAssemblyPaths(patterns
            .SelectMany(pattern => Directory.EnumerateFiles(artifactsRoot, pattern, SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// De-dups a set of candidate DLL paths by <see cref="AssemblyName"/> identity, preferring
    /// non-<c>Benchmark</c> paths and then shorter paths when identities collide.
    /// </summary>
    public static List<string> SelectPreferredAssemblyPaths(IEnumerable<string> candidates)
    {
        var byIdentity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string identity;
            try
            {
                identity = AssemblyName.GetAssemblyName(path).Name ?? Path.GetFileNameWithoutExtension(path);
            }
            catch
            {
                identity = Path.GetFileNameWithoutExtension(path);
            }

            if (!byIdentity.TryGetValue(identity, out var current))
            {
                byIdentity[identity] = path;
                continue;
            }

            if (IsPreferredPath(path, current))
            {
                byIdentity[identity] = path;
            }
        }

        return byIdentity.Values
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extracts the human-facing extension name (<c>"login"</c>, <c>"compute"</c>, ...) from
    /// an assembly name by trimming one of <paramref name="knownPrefixes"/>. Returns <c>false</c>
    /// when no prefix matches.
    /// </summary>
    public static bool TryGetExtensionName(string assemblyName, IEnumerable<string> knownPrefixes, out string extensionName)
    {
        foreach (var prefix in knownPrefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.Ordinal))
            {
                extensionName = assemblyName[prefix.Length..].ToLowerInvariant();
                return true;
            }
        }

        extensionName = string.Empty;
        return false;
    }

    private static bool IsPreferredPath(string candidate, string current)
    {
        var sep = Path.DirectorySeparatorChar;
        var candidateIsBenchmark = candidate.Contains($"{sep}Benchmark{sep}", StringComparison.OrdinalIgnoreCase);
        var currentIsBenchmark = current.Contains($"{sep}Benchmark{sep}", StringComparison.OrdinalIgnoreCase);
        if (candidateIsBenchmark != currentIsBenchmark)
        {
            return !candidateIsBenchmark;
        }

        // Prefer shorter canonical paths when both are same class (usually the primary artifact path).
        if (candidate.Length != current.Length)
        {
            return candidate.Length < current.Length;
        }

        return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) < 0;
    }
}
