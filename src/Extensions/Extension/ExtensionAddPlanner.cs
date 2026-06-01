// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension;

/// <summary>
/// Pure leaf-conflict planner extracted from <see cref="Processors.AddExtensionProcessor"/> so the
/// Shape B contract (§11.7 — same verb across packages is fine, same <c>(verb, path)</c> tuple is
/// not) can be exercised in tests without driving NuGet extraction or the registry filesystem.
/// </summary>
/// <remarks>
/// All inputs are already-resolved values supplied by <c>Add.cs</c>:
/// <list type="bullet">
///   <item><c>packageId</c> — the candidate's NuGet id (used for self-upsert detection).</item>
///   <item><c>verbs</c> — the candidate's declared <c>[ExtensionVerb]</c> set (read by <see cref="ExtensionVerbReader"/>).</item>
///   <item><c>cliLeafKeys</c> — the candidate's <c>s_cliTable</c> keys (read by <see cref="LeafReader"/>).</item>
///   <item><c>registry</c> — the current registry state to validate against.</item>
/// </list>
/// On success returns the contribution rows the caller should persist; on conflict throws the
/// same <see cref="CirrusErrorException"/> the production install path throws (LeafConflict
/// exit 5). Self-upsert is allowed: when a package id already owns a tuple, re-adding the same
/// id is a no-conflict.
/// </remarks>
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

        foreach (var leafKey in cliLeafKeys)
        {
            var (leafVerb, leafPath) = LeafReader.SplitLeafKey(leafKey);

            // First token must be a declared [ExtensionVerb] (§11.2, Q10).
            if (!declaredVerbSet.Contains(leafVerb))
            {
                throw CirrusErrorException.LeafConflict(
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

            // Built-in collision: built-ins occupy (verb, "") so an empty-path leaf for a
            // reserved verb is rejected. (The verb-level check upstream catches the common
            // case; this is defense-in-depth.)
            if (BuiltInExtensions.IsBuiltInVerb(leafVerb) && string.IsNullOrEmpty(leafPath))
            {
                throw CirrusErrorException.LeafConflict(
                    $"Extension '{packageId}' contributes root leaf '{leafKey}', which is reserved for " +
                    $"the built-in '{leafVerb}' extension.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["leafKey"] = leafKey,
                    });
            }

            // Registry collision: another installed package already owns the (verb, path) tuple.
            // Self-upsert is allowed (re-adding the same id is the upgrade path).
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
                        throw CirrusErrorException.LeafConflict(
                            $"Extension '{packageId}' wants to contribute leaf '{leafKey}', but it is already " +
                            $"owned by '{existingPackageId}'. Remove the existing extension first: " +
                            $"azv2 extension remove {existingPackageId}",
                            new Dictionary<string, object?>
                            {
                                ["packageId"] = packageId,
                                ["leafKey"] = leafKey,
                                ["verb"] = leafVerb,
                                ["path"] = leafPath,
                                ["existingPackageId"] = existingPackageId,
                            });
                    }
                }
            }

            contributions.Add(new Contribution { Verb = leafVerb, Path = leafPath });
        }

        return contributions;
    }
}
