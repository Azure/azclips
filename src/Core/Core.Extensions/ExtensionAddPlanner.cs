// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Core.Extensions;

/// <summary>
/// Pure leaf-conflict planner extracted from the extension-add processor for
/// testability. Validates (verb, path) tuples against the registry; same verb across packages
/// is fine, same (verb, path) is not. Self-upsert is allowed.
/// </summary>
public static class ExtensionAddPlanner
{
    public static IReadOnlyList<Contribution> PlanContributions(
        string packageId,
        IReadOnlyList<string> verbs,
        IReadOnlyList<string> cliLeafKeys,
        ExtensionRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(verbs);
        ArgumentNullException.ThrowIfNull(cliLeafKeys);
        ArgumentNullException.ThrowIfNull(registry);

        var declaredVerbSet = new HashSet<string>(verbs, StringComparer.OrdinalIgnoreCase);
        var contributions = new List<Contribution>();

        // Batch all registry-side (verb, path) collisions into a single LeafConflict so operators
        // get a complete diagnosis in one round-trip. Undeclared-verb / built-in-root checks
        // remain first-error-wins (usage errors in the candidate's own code).
        var registryConflicts = new List<Dictionary<string, object?>>();

        foreach (var leafKey in cliLeafKeys)
        {
            var (leafVerb, leafPath) = LeafReader.SplitLeafKey(leafKey);

            // First token must be a declared [ExtensionVerb].
            if (!declaredVerbSet.Contains(leafVerb))
            {
                throw AzclipsErrorException.LeafConflict(
                    $"Extension '{packageId}' contributes leaf '{leafKey}' whose first token is '{leafVerb}', " +
                    $"but only verbs [{string.Join(", ", verbs)}] are declared via [ExtensionVerb]. " +
                    $"Every s_cliTable key must start with a declared verb.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["declaredVerbs"] = verbs,
                        ["leafKey"] = leafKey,
                        ["leafVerb"] = leafVerb,
                    });
            }

            // Registry collision: another installed package already owns the (verb, path) tuple.
            // Self-upsert is allowed (re-adding the same id is the upgrade path). On hit we
            // record the row and keep walking — the throw happens once, after the loop.
            string? collidingExistingPackageId = null;
            foreach (var (existingPackageId, existingEntry) in registry.Extensions)
            {
                if (string.Equals(existingPackageId, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (var existingContribution in existingEntry.Contributions)
                {
                    if (string.Equals(existingContribution.Verb, leafVerb, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(existingContribution.Path, leafPath, StringComparison.OrdinalIgnoreCase))
                    {
                        collidingExistingPackageId = existingPackageId;
                        break;
                    }
                }
                if (collidingExistingPackageId is not null) { break; }
            }

            if (collidingExistingPackageId is not null)
            {
                registryConflicts.Add(new Dictionary<string, object?>
                {
                    ["leafKey"] = leafKey,
                    ["verb"] = leafVerb,
                    ["path"] = leafPath,
                    ["existingPackageId"] = collidingExistingPackageId,
                });
                // Skip adding this leaf to `contributions` — it's a duplicate that the throw
                // below will reject. Prevents `contributions` from carrying a row that would
                // never be persisted.
                continue;
            }

            contributions.Add(new Contribution { Verb = leafVerb, Path = leafPath });
        }

        if (registryConflicts.Count > 0)
        {
            // Stable, human-friendly summary: one line for the count, then a bullet per
            // collision. Keeps the message useful in plain-text terminals; the structured
            // `conflicts` array under `details` is what programmatic callers should consume.
            var summary = registryConflicts.Count == 1
                ? $"Extension '{packageId}' has 1 leaf conflict with already-installed extensions."
                : $"Extension '{packageId}' has {registryConflicts.Count} leaf conflicts with already-installed extensions.";
            var lines = new List<string> { summary };
            foreach (var c in registryConflicts)
            {
                lines.Add($"  - leaf '{c["leafKey"]}' is already owned by '{c["existingPackageId"]}'");
            }
            lines.Add("Remove the conflicting extension(s) first or rename the offending leaves.");

            throw AzclipsErrorException.LeafConflict(
                string.Join("\n", lines),
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["conflictCount"] = registryConflicts.Count,
                    ["conflicts"] = registryConflicts,
                });
        }

        return contributions;
    }
}
