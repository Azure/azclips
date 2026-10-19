// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.Loader;

namespace Core.Extensions
{
    /// <summary>
    /// Reads an extension's declared <c>[ExtensionVerb]</c> values and CLI command-table keys by
    /// loading it into a throwaway collectible <see cref="AssemblyLoadContext"/> and calling
    /// <c>GetCommandKeys(ClientType.CLI)</c> on its <c>IExtensionIndex</c>.
    /// </summary>
    /// <remarks>
    /// Used by <c>extension add</c> and the build-time sidecar generator. Unlike
    /// <see cref="ExtensionVerbReader"/>, this executes the extension's static initializers.
    /// </remarks>
    // TODO(multi-host): this reader is hardcoded to ClientType.CLI and named after CLI leaves
    // (ReadCliLeafKeys, s_cliTable). When a second host (e.g. TUI) needs leaf enumeration,
    // parameterize the ClientType passed to GetCommandKeys and rename to ReadLeafKeys.
    // The on-the-wire enum already exists in Core.Abstractions; the change is contained.
    public static class LeafReader
    {
        // Matched by name so Core.Abstractions needn't be referenced here.
        private const string ExtensionVerbAttributeFullName = "Core.Abstractions.ExtensionVerbAttribute";
        private const string ExtensionIndexInterfaceFullName = "Core.Abstractions.IExtensionIndex";

        /// <summary>
        /// Returns the CLI command-table keys (full-path form, e.g. <c>"vm create"</c>) declared by
        /// the extension in <paramref name="extensionDirectory"/>. See <see cref="ReadContract"/>.
        /// </summary>
        public static IReadOnlyList<string> ReadCliLeafKeys(string extensionDirectory) =>
            ReadContract(extensionDirectory).LeafKeys;

        /// <summary>
        /// Returns the verbs and CLI command-table keys (full-path form, e.g. <c>"vm create"</c>)
        /// declared by the extension in <paramref name="extensionDirectory"/>.
        /// </summary>
        /// <param name="extensionDirectory">Directory containing the extension's built DLL.</param>
        /// <param name="additionalProbeDirectories">
        /// Extra directories to resolve shared dependencies from when they aren't loaded by the host
        /// or copied next to the extension.
        /// </param>
        /// <returns>
        /// Both come from the <c>&lt;assembly name&gt;.ExtensionIndex</c> type, the only one the runtime
        /// loads; other types are ignored. Empty <c>LeafKeys</c> means the extension declares no
        /// commands; both empty means it declares no <c>[ExtensionVerb]</c> contract at all.
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// The index type is malformed or can't be instantiated, or <c>[ExtensionVerb]</c> is
        /// declared without a conventional index type.
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
                var loadedAssemblies = new List<Assembly>();
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

                    loadedAssemblies.Add(assembly);

                    // Mirrors CommandLoadingExtension.GetExtensionIndex, so only the type the runtime loads counts.
                    var indexType = assembly.GetType($"{assembly.GetName().Name}.ExtensionIndex");
                    if (indexType is not null)
                    {
                        return ReadIndexContract(indexType, dllPath);
                    }
                }

                // No conventional index. A decorated type elsewhere means a malformed extension;
                // otherwise there is no CLI contract.
                var orphanTypeName = FindVerbAttributedTypeName(loadedAssemblies);
                if (orphanTypeName is not null)
                {
                    throw new InvalidOperationException(
                        $"'{orphanTypeName}' declares [ExtensionVerb] but the extension has no " +
                        $"'<assembly name>.ExtensionIndex' type implementing IExtensionIndex, which is the " +
                        $"only type the runtime loads. The empty tuple is reserved for assemblies with no " +
                        $"declared CLI contract at all.");
                }

                return (Array.Empty<string>(), Array.Empty<string>());
            }
            finally
            {
                alc.Unload();
            }
        }

        private static (IReadOnlyList<string> Verbs, IReadOnlyList<string> LeafKeys) ReadIndexContract(
            Type indexType, string dllPath)
        {
            if (!indexType.GetInterfaces().Any(i => i.FullName == ExtensionIndexInterfaceFullName))
            {
                throw new InvalidOperationException(
                    $"'{indexType.FullName}' is the extension's conventional index type but does not " +
                    $"implement IExtensionIndex.");
            }

            // Same semantics as ExtensionVerbDeclarations.GetDeclaredVerbs.
            var verbs = new List<string>();
            var seenVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in CustomAttributeData.GetCustomAttributes(indexType))
            {
                if (attr.AttributeType.FullName != ExtensionVerbAttributeFullName) { continue; }
                if (attr.ConstructorArguments.Count > 0 &&
                    attr.ConstructorArguments[0].Value is string verb &&
                    seenVerbs.Add(verb))
                {
                    verbs.Add(verb);
                }
            }

            object? instance;
            try
            {
                instance = Activator.CreateInstance(indexType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate IExtensionIndex implementer '{indexType.FullName}' " +
                    $"from '{Path.GetFileName(dllPath)}'. The extension must have a public " +
                    $"parameterless constructor.", ex);
            }

            var method = indexType.GetMethod("GetCommandKeys", BindingFlags.Instance | BindingFlags.Public);
            if (instance is null || method is null) { return (verbs, Array.Empty<string>()); }

            // Pass enum value as int to avoid a hard ref to Core.Abstractions here.
            var clientTypeEnum = method.GetParameters()[0].ParameterType;
            var cliValue = Enum.Parse(clientTypeEnum, "CLI", ignoreCase: true);

            if (method.Invoke(instance, new[] { cliValue }) is not System.Collections.IEnumerable result)
            {
                return (verbs, Array.Empty<string>());
            }

            var keys = new List<string>();
            foreach (var item in result)
            {
                if (item is string s) { keys.Add(s); }
            }
            return (verbs, keys);
        }

        private static string? FindVerbAttributedTypeName(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                Type?[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (var type in types)
                {
                    if (type is not null &&
                        CustomAttributeData.GetCustomAttributes(type)
                            .Any(a => a.AttributeType.FullName == ExtensionVerbAttributeFullName))
                    {
                        return type.FullName;
                    }
                }
            }

            return null;
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
