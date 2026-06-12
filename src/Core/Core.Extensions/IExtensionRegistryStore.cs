// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// Persistence boundary for <see cref="ExtensionRegistry"/>.
    /// </summary>
    /// <remarks>
    /// Abstracted so tests can substitute an in-memory store and so future scopes
    /// (machine-wide, sandboxed) can plug in without touching callers.
    /// </remarks>
    public interface IExtensionRegistryStore
    {
        /// <summary>
        /// Load the registry. Returns an empty registry (schema = current) if no file exists yet.
        /// </summary>
        ExtensionRegistry Load();

        /// <summary>
        /// Persist the registry atomically. Creates parent directories if missing.
        /// </summary>
        void Save(ExtensionRegistry registry);
    }
}
