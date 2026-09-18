// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Agent.Tools.Help
{
    /// <summary>
    /// Pure command-lookup helpers over a set of <see cref="HelpManifest"/> — the
    /// name-matching backbone of <c>az agent tool help --name &lt;x&gt;</c>. Extracted
    /// from the CLI processor so the lookup/suggestion contracts are unit
    /// testable without the processor's assembly-loading dependencies.
    /// </summary>
    public static class HelpLookup
    {
        /// <summary>Max suggestions returned on a miss; keeps the not_found payload small.</summary>
        public const int MaxSuggestions = 8;

        /// <summary>
        /// Looks up a <see cref="HelpCommand"/> across all manifests by cliName or psName
        /// (case-insensitive). Returns null when no command matches.
        /// </summary>
        public static HelpCommand? FindCommand(IEnumerable<HelpManifest> manifests, string name)
        {
            foreach (var manifest in manifests)
            {
                foreach (var command in manifest.Commands)
                {
                    if (string.Equals(command.CliName, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command.PsName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return command;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// On a miss, offers commands whose cliName/psName contains the query (or vice
        /// versa) so a partial or slightly-off name — e.g. "vm" -> "vm create",
        /// "create" -> "vm create-basic" — can still be recovered instead of a dead end.
        /// Case-insensitive, de-duplicated, capped at <see cref="MaxSuggestions"/>.
        /// </summary>
        public static IReadOnlyList<string> FindSuggestions(IEnumerable<HelpManifest> manifests, string name)
        {
            var q = name.Trim();
            if (q.Length == 0) return Array.Empty<string>();

            var hits = new List<string>();
            foreach (var manifest in manifests)
            {
                foreach (var command in manifest.Commands)
                {
                    foreach (var candidate in new[] { command.CliName, command.PsName })
                    {
                        if (string.IsNullOrEmpty(candidate)) continue;
                        if (candidate.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            q.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!hits.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                                hits.Add(candidate);
                        }
                    }
                    if (hits.Count >= MaxSuggestions) return hits;
                }
            }
            return hits;
        }
    }
}
