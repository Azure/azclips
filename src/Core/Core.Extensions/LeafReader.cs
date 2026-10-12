// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.Loader;

namespace Core.Extensions
{
    /// <summary>
    /// Reads the contract an extension actually declares — its <c>[ExtensionVerb]</c> attributes
    /// and its CLI command-table keys (in full-path form) — by loading the extension into a
    /// throwaway collectible <see cref="AssemblyLoadContext"/>, instantiating its
    /// <c>IExtensionIndex</c> via reflection, and calling <c>GetCommandKeys(ClientType.CLI)</c>.
    /// This reads the real public contract rather than pattern-matching source conventions, so it
    /// can't drift from what the extension actually registers.
    /// </summary>
    /// <remarks>
    /// Used by <c>extension add</c> to enumerate the candidate's contributions before committing
    /// the registry write so leaf-level conflicts can be detected up front, and by the build-time
    /// sidecar generator (<c>ExtensionSidecar.targets</c>) to emit <c>&lt;PackageId&gt;.commands.json</c>.
    /// Unlike <see cref="ExtensionVerbReader"/> (which uses <see cref="MetadataLoadContext"/> for
    /// code-safe attribute reads), this reader executes static initializers — that's required to
    /// materialize the dictionary, but the ALC is unloaded after the read so any side effects are
    /// scoped to a single GC cycle.
    /// </remarks>
    // TODO(multi-host): this reader is hardcoded to ClientType.CLI and named after CLI leaves
    // (ReadCliLeafKeys, s_cliTable). When a second host (e.g. TUI) needs leaf enumeration,
    // parameterize the ClientType passed to GetCommandKeys and rename to ReadLeafKeys.
    // The on-the-wire enum already exists in Core.Abstractions; the change is contained.
    public static class LeafReader
    {
        // The attribute name string we use to find the IExtensionIndex implementer without a
        // hard reference dance. Mirrors ExtensionVerbReader's pattern.
        private const string ExtensionVerbAttributeFullName = "Core.Abstractions.ExtensionVerbAttribute";
        private const string ExtensionIndexInterfaceFullName = "Core.Abstractions.IExtensionIndex";

        /// <summary>
        /// Load the extension at <paramref name="extensionDirectory"/> in a throwaway ALC and
        /// return its CLI command-table keys (full-path form, e.g. <c>"vm create"</c>).
        /// </summary>
        /// <returns>
        /// The list of declared leaf keys, or an empty list if the extension exposes no CLI
        /// commands (or pre-dates the <c>GetCommandKeys</c> interface member and inherits the
        /// default empty implementation).
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">Install directory does not exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// Extension declares <c>[ExtensionVerb]</c> but no <c>IExtensionIndex</c> implementer is
        /// found, or the implementer fails to instantiate.
        /// </exception>
        public static IReadOnlyList<string> ReadCliLeafKeys(string extensionDirectory) =>
            ReadContract(extensionDirectory).LeafKeys;

        /// <summary>
        /// Load the extension at <paramref name="extensionDirectory"/> in a throwaway ALC and
        /// return both its declared <c>[ExtensionVerb]</c> values and its CLI command-table keys
        /// (full-path form, e.g. <c>"vm create"</c>).
        /// </summary>
        /// <param name="extensionDirectory">Directory containing the extension's own built DLL.</param>
        /// <param name="additionalProbeDirectories">
        /// Extra directories to search for the extension's shared dependencies (e.g.
        /// Core.Abstractions, ExtensionRuntime) when they aren't already loaded by the host and
        /// aren't copied alongside the extension's own DLL — as is the case for in-tree
        /// extensions, which reference those assemblies with <c>ExcludeAssets="runtime"</c> and
        /// rely on the real CLI host to provide them at install time.
        /// </param>
        /// <returns>
        /// Verbs and leaf keys declared by the extension, or both empty if it exposes no CLI
        /// commands (or pre-dates the <c>GetCommandKeys</c> interface member and inherits the
        /// default empty implementation).
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">Install directory does not exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// Extension declares <c>[ExtensionVerb]</c> but no <c>IExtensionIndex</c> implementer is
        /// found, or the implementer fails to instantiate.
        /// </exception>
        public static (IReadOnlyList<string> Verbs, IReadOnlyList<string> LeafKeys) ReadContract(
            string extensionDirectory, params string[] additionalProbeDirectories)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(extensionDirectory);

            if (!Directory.Exists(extensionDirectory))
            {
                throw new DirectoryNotFoundException($"Extension directory not found: {extensionDirectory}");
            }

            var dllPaths = Directory.GetFiles(extensionDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            if (dllPaths.Length == 0)
            {
                return (Array.Empty<string>(), Array.Empty<string>());
            }

            // Collectible ALC; unloaded after read so extension assembly bytes are GC-eligible.
            var alc = new ThrowawayLoadContext(extensionDirectory, additionalProbeDirectories);
            try
            {
                foreach (var dllPath in dllPaths)
                {
                    Assembly assembly;
                    try
                    {
                        assembly = alc.LoadFromAssemblyPath(dllPath);
                    }
                    catch (BadImageFormatException)
                    {
                        continue;
                    }
                    catch (FileLoadException)
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t is not null).ToArray()!;
                    }

                    // Find the [ExtensionVerb]-decorated type (conventionally the IExtensionIndex implementer).
                    foreach (var type in types)
                    {
                        if (type is null) { continue; }

                        var verbs = new List<string>();
                        foreach (var attr in CustomAttributeData.GetCustomAttributes(type))
                        {
                            if (attr.AttributeType.FullName != ExtensionVerbAttributeFullName) { continue; }
                            if (attr.ConstructorArguments.Count > 0 &&
                                attr.ConstructorArguments[0].Value is string verb)
                            {
                                verbs.Add(verb);
                            }
                        }
                        if (verbs.Count == 0) { continue; }

                        // Verify it actually implements IExtensionIndex (defensive — but the
                        // verb attribute alone is not the contract).
                        var implementsIndex = type.GetInterfaces()
                            .Any(i => i.FullName == ExtensionIndexInterfaceFullName);
                        if (!implementsIndex) { continue; }

                        object? instance;
                        try
                        {
                            instance = Activator.CreateInstance(type);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Could not instantiate IExtensionIndex implementer '{type.FullName}' " +
                                $"from '{Path.GetFileName(dllPath)}'. The extension must have a public " +
                                $"parameterless constructor.", ex);
                        }

                        if (instance is null) { continue; }

                        var method = type.GetMethod("GetCommandKeys", BindingFlags.Instance | BindingFlags.Public);
                        if (method is null) { return (verbs, Array.Empty<string>()); }

                        // Pass enum value as int to avoid a hard ref to Core.Abstractions here.
                        var clientTypeEnum = method.GetParameters()[0].ParameterType;
                        var cliValue = Enum.Parse(clientTypeEnum, "CLI", ignoreCase: true);

                        var result = method.Invoke(instance, new[] { cliValue }) as System.Collections.IEnumerable;
                        if (result is null) { return (verbs, Array.Empty<string>()); }

                        var keys = new List<string>();
                        foreach (var item in result)
                        {
                            if (item is string s) { keys.Add(s); }
                        }
                        return (verbs, keys);
                    }
                }
            }
            finally
            {
                alc.Unload();
            }

            // No verb-decorated type found in any DLL. Caller will surface this via the
            // existing missing-[ExtensionVerb] error path.
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        /// <summary>
        /// Parse a full-path leaf key (e.g. <c>"vm create"</c>) into <c>(verb, path)</c>. The verb
        /// is always the first whitespace-separated token; the path is everything after, or the
        /// empty string for root-level leaves like <c>"login"</c>.
        /// </summary>
        public static (string Verb, string Path) SplitLeafKey(string leafKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(leafKey);
            var trimmed = leafKey.Trim();
            var spaceIdx = trimmed.IndexOf(' ');
            return spaceIdx < 0
                ? (trimmed, string.Empty)
                : (trimmed[..spaceIdx], trimmed[(spaceIdx + 1)..].Trim());
        }

        private sealed class ThrowawayLoadContext : AssemblyLoadContext
        {
            private readonly string _extensionDirectory;
            private readonly IReadOnlyList<string> _additionalProbeDirectories;

            public ThrowawayLoadContext(string extensionDirectory, IReadOnlyList<string>? additionalProbeDirectories = null)
                : base(name: $"LeafReader[{extensionDirectory}]", isCollectible: true)
            {
                _extensionDirectory = extensionDirectory;
                _additionalProbeDirectories = additionalProbeDirectories ?? Array.Empty<string>();
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                // Prefer host's copy for shared abstractions so type identity matches across the boundary.
                var hostLoaded = Default.Assemblies
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.Ordinal));
                if (hostLoaded is not null)
                {
                    return hostLoaded;
                }

                var candidate = Path.Combine(_extensionDirectory, $"{assemblyName.Name}.dll");
                if (File.Exists(candidate))
                {
                    return LoadFromAssemblyPath(candidate);
                }

                // Not every host pre-loads the extension's shared dependencies (e.g. a build-time
                // task process, unlike the real CLI host, never loads Core.Abstractions itself) —
                // fall back to any extra directories the caller knows about.
                foreach (var dir in _additionalProbeDirectories)
                {
                    var extraCandidate = Path.Combine(dir, $"{assemblyName.Name}.dll");
                    if (File.Exists(extraCandidate))
                    {
                        return LoadFromAssemblyPath(extraCandidate);
                    }
                }

                return null;
            }
        }
    }
}
