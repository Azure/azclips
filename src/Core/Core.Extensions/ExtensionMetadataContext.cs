// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Core.Extensions
{
    /// <summary>
    /// Builds a <see cref="MetadataLoadContext"/> that can resolve TypeRefs for an extension's
    /// DLLs without executing any of its code. Shared by readers that scan extension metadata
    /// at install/load time (e.g. <c>ExtensionVerbReader</c>, <c>CompatibilityGuard</c>).
    /// </summary>
    public static class ExtensionMetadataContext
    {
        /// <summary>
        /// Creates a metadata-only load context for <paramref name="extensionDirectory"/> and
        /// returns the DLL paths in that directory. Resolver covers runtime BCL + host base
        /// directory + the extension directory itself.
        /// </summary>
        /// <param name="extensionDirectory">Directory containing the extension's DLLs. Must exist.</param>
        /// <param name="dllPaths">DLLs found in <paramref name="extensionDirectory"/>; empty if none.</param>
        /// <returns>An owned <see cref="MetadataLoadContext"/>; caller must dispose.</returns>
        public static MetadataLoadContext Create(string extensionDirectory, out string[] dllPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(extensionDirectory);

            if (!Directory.Exists(extensionDirectory))
            {
                throw new DirectoryNotFoundException($"Extension directory not found: {extensionDirectory}");
            }

            dllPaths = Directory.GetFiles(extensionDirectory, "*.dll", SearchOption.TopDirectoryOnly);

            var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            var hostAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "*.dll");
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in runtimeAssemblies) allPaths.Add(p);
            foreach (var p in hostAssemblies) allPaths.Add(p);
            foreach (var p in dllPaths) allPaths.Add(p);

            var resolver = new PathAssemblyResolver(allPaths);
            return new MetadataLoadContext(resolver);
        }
    }
}
