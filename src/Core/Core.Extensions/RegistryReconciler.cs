// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// Describes the result of comparing a loaded <see cref="ExtensionRegistry"/> against the
    /// actual contents of the extensions install root.
    /// </summary>
    /// <param name="Live">Registry containing only entries whose install directory still exists.</param>
    /// <param name="Stale">Entries whose recorded install path is missing on disk.</param>
    public sealed record ReconcileResult(
        ExtensionRegistry Live,
        IReadOnlyList<StaleEntry> Stale);

    /// <summary>
    /// A registry entry whose payload (the package's version folder) is missing from disk.
    /// Typically caused by manual cleanup (<c>rm -rf ~/.cirrus/extensions/...</c>) or a partial
    /// install that crashed before commit.
    /// </summary>
    public sealed record StaleEntry(
        string PackageId,
        string Version,
        string ExpectedPath,
        string Reason);

    /// <summary>
    /// DEPRECATED in §5.2: No longer needed. DiscoverySession now owns determining installed state
    /// by walking the disk directly; registry reconciliation is implicit and automatic.
    /// Kept as a no-op for back-compat; callers should migrate to reading discovery output instead.
    /// </summary>
    public static class RegistryReconciler
    {
        /// <summary>
        /// DEPRECATED: Returns the input registry unchanged. No disk reconciliation is performed.
        /// DiscoverySession now handles this role automatically.
        /// </summary>
        [Obsolete("Use DiscoverySession to enumerate installed state. Reconciliation is no longer performed. Will be removed in §5.3.", error: false)]
        public static ReconcileResult Reconcile(ExtensionRegistry registry, string installRoot)
        {
            ArgumentNullException.ThrowIfNull(registry);
            // installRoot is no longer used; kept for signature compat.
            
            // Return all entries as "live"; no disk checking is performed.
            // The actual installed state is now determined by DiscoverySession at runtime.
            return new ReconcileResult(registry, new List<StaleEntry>());
        }
    }
}
