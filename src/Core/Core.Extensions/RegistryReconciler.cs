// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// Describes the result of comparing a loaded <see cref="ExtensionRegistry"/> against the
    /// actual contents of the extensions install root. Phase 8.5 (registry hardening).
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
    /// Filesystem-level reconciliation between <see cref="ExtensionRegistry"/> and the on-disk
    /// install root. Drops entries whose install directory is gone so the dispatcher never
    /// routes to a package that's no longer there.
    /// </summary>
    /// <remarks>
    /// Per <c>docs/spec/extension-management.md §11.11</c> (Phase 8.5): non-destructive — the
    /// caller decides whether to write the live view back to disk or just inform the user.
    /// </remarks>
    public static class RegistryReconciler
    {
        /// <summary>
        /// Build a <see cref="ReconcileResult"/> by checking <c>{installRoot}/{packageId}/{version}/</c>
        /// for every entry in <paramref name="registry"/>. Entries with missing payload are moved
        /// to the <see cref="ReconcileResult.Stale"/> list; the rest land in
        /// <see cref="ReconcileResult.Live"/>.
        /// </summary>
        public static ReconcileResult Reconcile(ExtensionRegistry registry, string installRoot)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);

            var live = new ExtensionRegistry
            {
                SchemaVersion = registry.SchemaVersion,
                Extensions = new Dictionary<string, ExtensionRegistryEntry>(StringComparer.OrdinalIgnoreCase),
            };
            var stale = new List<StaleEntry>();

            foreach (var (packageId, entry) in registry.Extensions)
            {
                var expected = Path.Combine(installRoot, packageId, entry.Version);
                if (Directory.Exists(expected))
                {
                    live.Extensions[packageId] = entry;
                }
                else
                {
                    stale.Add(new StaleEntry(
                        PackageId: packageId,
                        Version: entry.Version,
                        ExpectedPath: expected,
                        Reason: "install directory does not exist"));
                }
            }

            return new ReconcileResult(live, stale);
        }
    }
}
