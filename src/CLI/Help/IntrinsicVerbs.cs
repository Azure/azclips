// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// Canonical set of intrinsic host verbs: commands implemented by the CLI host itself on the
/// cold-path (no engine, no extension load), not by any installed extension — currently
/// <c>version</c> (<c>az version</c> / <c>az --version</c>). Their help still comes from the
/// markdown corpus so they appear in <c>az --help</c> and <c>az &lt;verb&gt; --help</c> like any
/// extension verb, and help-doc tooling must treat them as known CLI groups. Extend as new
/// intrinsics (e.g. <c>upgrade</c>) land.
/// </summary>
public static class IntrinsicVerbs
{
    /// <summary>Case-insensitive set of intrinsic host verbs (the single source of truth).</summary>
    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "version" };

    /// <summary>
    /// Resolves the canonical intrinsic verb for a group token, accepting either the bare verb
    /// (<c>version</c>) or its equivalent top-level-flag form (<c>--version</c>) — the two syntaxes the
    /// runtime dispatches for these host commands (e.g. <c>az version</c> / <c>az --version</c>). On
    /// success, <paramref name="verb"/> is the canonical verb as stored in <see cref="All"/> (stable
    /// casing for case-sensitive lookups such as help-doc folders); on failure it is the unresolved
    /// candidate.
    /// </summary>
    public static bool TryResolve(string token, out string verb)
    {
        ArgumentNullException.ThrowIfNull(token);

        var candidate = token.StartsWith("--", StringComparison.Ordinal) ? token[2..] : token;

        foreach (var known in All)
        {
            if (string.Equals(known, candidate, StringComparison.OrdinalIgnoreCase))
            {
                verb = known;
                return true;
            }
        }

        verb = candidate;
        return false;
    }
}
