// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Core.Extensions
{
    /// <summary>
    /// Reads the <c>[ExtensionVerb]</c> attribute from an extension's DLLs without loading or
    /// executing any of its code.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="MetadataLoadContext"/> with <see cref="CustomAttributeData"/> so we
    /// never instantiate the extension's <c>IExtensionIndex</c>. This is critical at
    /// <c>extension add</c> time: a malicious or merely buggy extension shouldn't be able to
    /// run code during verb-conflict validation.
    /// </remarks>
    public static class ExtensionVerbReader
    {
        // Match by full name string so we don't have to load Core.Abstractions into the
        // metadata-only context. Keep in sync with ExtensionVerbAttribute.
        private const string AttributeFullName = "Core.Abstractions.ExtensionVerbAttribute";

        /// <summary>
        /// Scan all DLLs in <paramref name="extensionDirectory"/> for an <c>[ExtensionVerb]</c>
        /// attribute and return the declared verb.
        /// </summary>
        /// <returns>The verb string, or <c>null</c> if no DLL declares one.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if multiple DLLs declare different verbs (an extension can only own one verb).
        /// </exception>
        public static string? ReadVerb(string extensionDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(extensionDirectory);

            if (!Directory.Exists(extensionDirectory))
            {
                throw new DirectoryNotFoundException($"Extension directory not found: {extensionDirectory}");
            }

            var dllPaths = Directory.GetFiles(extensionDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            if (dllPaths.Length == 0)
            {
                return null;
            }

            // MetadataLoadContext needs access to runtime BCL assemblies to resolve TypeRefs,
            // even though we only read CustomAttributeData. Include them in the resolver,
            // plus the host's own directory so references like Core.Abstractions resolve.
            var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            var hostAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "*.dll");
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in runtimeAssemblies) allPaths.Add(p);
            foreach (var p in hostAssemblies)    allPaths.Add(p);
            foreach (var p in dllPaths)          allPaths.Add(p);

            var resolver = new PathAssemblyResolver(allPaths);
            using var mlc = new MetadataLoadContext(resolver);

            string? foundVerb = null;
            string? foundIn = null;

            foreach (var dllPath in dllPaths)
            {
                Assembly assembly;
                try
                {
                    assembly = mlc.LoadFromAssemblyPath(dllPath);
                }
                catch (BadImageFormatException)
                {
                    // Native or otherwise non-managed; skip.
                    continue;
                }

                foreach (var type in SafeGetTypes(assembly))
                {
                    foreach (var attr in type.GetCustomAttributesData())
                    {
                        if (attr.AttributeType.FullName != AttributeFullName)
                        {
                            continue;
                        }

                        if (attr.ConstructorArguments.Count == 0 ||
                            attr.ConstructorArguments[0].Value is not string verb)
                        {
                            continue;
                        }

                        if (foundVerb is not null && !string.Equals(foundVerb, verb, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Extension at '{extensionDirectory}' declares multiple verbs " +
                                $"('{foundVerb}' in '{foundIn}', '{verb}' in '{Path.GetFileName(dllPath)}'). " +
                                $"Shape A requires exactly one verb per extension.");
                        }

                        foundVerb = verb;
                        foundIn = Path.GetFileName(dllPath);
                    }
                }
            }

            return foundVerb;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Return whatever types we did manage to load; skip the broken ones.
                return ex.Types.Where(t => t is not null)!;
            }
        }
    }
}
