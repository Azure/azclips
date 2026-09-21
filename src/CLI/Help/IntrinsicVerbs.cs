// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// A single intrinsic host verb: a command implemented by the CLI host itself on the cold-path
/// (no engine, no extension load) rather than by any installed extension.
/// </summary>
/// <param name="Name">
/// Canonical, case-stable verb name (e.g. <c>version</c>). Case-sensitive lookups such as help-doc
/// folders rely on this casing, so it is authoritative.
/// </param>
/// <param name="AllowFlagForm">
/// When <see langword="true"/>, the verb may also be written as the top-level flag <c>--{Name}</c>
/// (e.g. <c>az --version</c> alongside <c>az version</c>). When <see langword="false"/>, only the
/// bare verb form is valid and the <c>--{Name}</c> shape is not recognized as this verb.
/// </param>
public sealed record IntrinsicVerb(string Name, bool AllowFlagForm);

/// <summary>
/// Canonical set of intrinsic host verbs: commands implemented by the CLI host itself on the
/// cold-path (no engine, no extension load), not by any installed extension — currently
/// <c>version</c> (<c>az version</c> / <c>az --version</c>). Their help still comes from the
/// markdown corpus so they appear in <c>az --help</c> and <c>az &lt;verb&gt; --help</c> like any
/// extension verb, and help-doc tooling must treat them as known CLI groups. Add new intrinsics
/// (e.g. <c>upgrade</c>) to <see cref="All"/>, choosing <see cref="IntrinsicVerb.AllowFlagForm"/>
/// per verb — set it to <see langword="false"/> for verbs that only support the bare form.
/// </summary>
public static class IntrinsicVerbs
{
    /// <summary>Canonical name of the <c>version</c> intrinsic (<c>az version</c> / <c>az --version</c>).</summary>
    public const string Version = "version";
    public const string Troubleshoot = "troubleshoot";

    private static readonly IReadOnlyList<IntrinsicVerb> s_all =
    [
        new IntrinsicVerb(Version, AllowFlagForm: true),
        new IntrinsicVerb(Troubleshoot, AllowFlagForm: false),
    ];

    private static readonly IReadOnlyDictionary<string, IntrinsicVerb> s_byName =
        s_all.ToDictionary(static verb => verb.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>All intrinsic host verbs and their metadata (the single source of truth).</summary>
    public static IReadOnlyList<IntrinsicVerb> All => s_all;

    internal static RuntimeHelpCommand CreateHelp(string canonicalVerb)
    {
        var markdown = MarkdownCommandHelpProvider.Get(canonicalVerb, string.Empty);
        var parameters = canonicalVerb == Troubleshoot
            ? new[]
            {
                new RuntimeHelpParameter("id", [], false, "Analyze the saved failure with this ID. Omit to use the latest record.",
                    "Saved failure ID.", [], "Default", false, false),
                new RuntimeHelpParameter("list", [], false, "List saved failures without calling AI. Cannot be combined with --id.",
                    "List saved failures.", [], "Default", false, false),
            }
            : Array.Empty<RuntimeHelpParameter>();
        return new(
            canonicalVerb, markdown.Summary, markdown.Description, parameters,
            markdown.Examples.Where(example => !example.IsPowerShell)
                .Select(example => new RuntimeHelpExample(example.Title, example.Command)).ToArray(),
            []);
    }

    /// <summary>
    /// Resolves the canonical intrinsic verb for a group token. The bare verb (<c>version</c>) is
    /// always accepted; the equivalent top-level-flag form (<c>--version</c>) is accepted only when the
    /// resolved verb opts in via <see cref="IntrinsicVerb.AllowFlagForm"/>. On success,
    /// <paramref name="verb"/> is the canonical verb as stored in <see cref="All"/> (stable casing for
    /// case-sensitive lookups such as help-doc folders); on failure it is the original
    /// <paramref name="token"/> unchanged, so callers can echo exactly what the user typed.
    /// </summary>
    public static bool TryResolve(string token, out string verb)
    {
        ArgumentNullException.ThrowIfNull(token);

        var isFlagForm = token.StartsWith("--", StringComparison.Ordinal);
        var candidate = isFlagForm ? token[2..] : token;

        if (s_byName.TryGetValue(candidate, out var intrinsic) && (!isFlagForm || intrinsic.AllowFlagForm))
        {
            verb = intrinsic.Name;
            return true;
        }

        verb = token;
        return false;
    }
}
