// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Core.Cli.Version;
using Core.Extensions;

namespace Core.Cli.Parser;

/// <summary>
/// One-pass classifier for user-facing global CLI flags (<c>--debug</c>, <c>--output</c>,
/// <c>--no-cache</c>, <c>--help</c> family, <c>--version</c> family). Runs after DI init and before
/// <see cref="ExtensionDispatcher.Parse"/>; the resulting <see cref="GlobalArgs"/> record replaces
/// the ad-hoc <c>ParseXxxFlag</c> helpers and the <c>DispatchHelpException</c> /
/// <c>DispatchVersionException</c> control-flow exceptions.
/// </summary>
/// <remarks>
/// Not part of this parser (intentional):
/// <list type="bullet">
///   <item><c>--upload-telemetry</c> — pre-DI process-shape flag; the detached telemetry uploader
///     child skips DI entirely, so it short-circuits before this parser runs.</item>
///   <item><c>--print-probe</c> — diagnostic with its own mini-grammar
///     (<c>--list</c>, <c>&lt;name&gt;</c>). See <see cref="PrintProbe"/>.</item>
///   <item>Shell tab-completion generation — needs DI-resolved services.</item>
/// </list>
/// </remarks>
internal static class GlobalArgParser
{
    private const string HelpSubcommand = "help";

    private static readonly string[] s_helpFlags = ["--help", "-h", "-?", "?"];

    /// <summary>
    /// Classifies <paramref name="args"/> into a <see cref="GlobalArgs"/> record in one pass.
    /// Per-token flag recognition dispatches through <see cref="KnownGlobalFlags.All"/>; the
    /// positional-list post-pass handles help / version classification since those need to
    /// examine the whole stream, not a single token.
    /// </summary>
    /// <exception cref="AzclipsErrorException">
    /// Raised as a usage error (exit code 2) when <c>--output</c> is missing a value or receives an
    /// unrecognized value.
    /// </exception>
    public static GlobalArgs Parse(ReadOnlySpan<string> args)
    {
        bool isBareInvocation = args.Length == 0;

        if (args.Length == 0)
        {
            return new GlobalArgs(
                Positional: Array.Empty<string>(),
                IsBareInvocation: true,
                Debug: false,
                Output: null,
                NoCache: false,
                Help: new HelpRequest(Array.Empty<string>()),
                Version: null);
        }

        var positional = new List<string>(args.Length);
        var builder = new GlobalArgsBuilder();

        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];
            var match = MatchKnownFlag(token);

            if (match?.Action is not null)
            {
                var consumed = match.Action.Apply(builder, args, i);
                i += consumed - 1;
                continue;
            }

            positional.Add(token);
        }

        var help = TryClassifyHelp(positional);
        var version = help is null ? TryClassifyVersion(positional) : null;

        return new GlobalArgs(
            Positional: positional,
            IsBareInvocation: isBareInvocation,
            Debug: builder.Debug,
            Output: builder.Output,
            NoCache: builder.NoCache,
            Help: help,
            Version: version);
    }

    // Case-insensitive match of `token` (or its `--name=value` prefix) against
    // KnownGlobalFlags.All by name or alias. Returns null when no flag matches.
    private static KnownGlobalFlag? MatchKnownFlag(string token)
    {
        var lookup = token;
        var eq = token.IndexOf('=');
        if (eq >= 0)
        {
            lookup = token[..eq];
        }

        foreach (var flag in KnownGlobalFlags.All)
        {
            if (flag.Name.Equals(lookup, StringComparison.OrdinalIgnoreCase))
            {
                return flag;
            }

            foreach (var alias in flag.Aliases)
            {
                if (alias.Equals(lookup, StringComparison.OrdinalIgnoreCase))
                {
                    return flag;
                }
            }
        }

        return null;
    }

    // Recognized shapes: `az help ...` or `az <verb> ... --help`. `az --help <trailing>` intentionally falls through.
    private static HelpRequest? TryClassifyHelp(IReadOnlyList<string> positional)
    {
        if (positional.Count == 0)
        {
            // `az --debug` alone → root help; preserves pre-refactor behavior.
            return new HelpRequest(Array.Empty<string>());
        }

        if (positional[0].Equals(HelpSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            if (positional.Count == 1)
            {
                return new HelpRequest(Array.Empty<string>());
            }
            // `az help ""` falls through to the dispatcher's whitespace guard.
            if (string.IsNullOrWhiteSpace(positional[1]))
            {
                return null;
            }
            var subPath = new List<string>();
            for (int i = 1; i < positional.Count; i++)
            {
                var token = positional[i];
                // `?` is a help token pre-refactor; skip it here (don't treat it as a path segment).
                if (IsHelpFlag(token))
                {
                    continue;
                }
                if (token.StartsWith('-'))
                {
                    break;
                }
                subPath.Add(token);
            }
            return new HelpRequest(subPath);
        }

        // Bare top-level help flag: `az --help`, `az -h`, `az -?`, `az ?`.
        if (positional.Count == 1 && IsHelpFlag(positional[0]))
        {
            return new HelpRequest(Array.Empty<string>());
        }

        // Trailing / interspersed help flag: `az <verb> [path...] --help`.
        bool trailingHelp = false;
        for (int i = 1; i < positional.Count; i++)
        {
            if (IsHelpFlag(positional[i]))
            {
                trailingHelp = true;
                break;
            }
        }
        if (!trailingHelp)
        {
            return null;
        }

        var stripped = new List<string>(positional.Count);
        foreach (var token in positional)
        {
            if (!IsHelpFlag(token))
            {
                stripped.Add(token);
            }
        }
        if (stripped.Count == 0 || string.IsNullOrWhiteSpace(stripped[0]))
        {
            // No verb survived, or verb is empty/whitespace — fall through so the dispatcher's
            // guard still surfaces the correct error rather than us fabricating a help request.
            return null;
        }
        var path = new List<string> { stripped[0] };
        for (int i = 1; i < stripped.Count; i++)
        {
            if (stripped[i].StartsWith('-'))
            {
                break;
            }
            path.Add(stripped[i]);
        }
        return new HelpRequest(path);
    }

    // Only exact single-token shapes dispatch; trailing tokens fall through to the dispatcher's usage error.
    private static VersionRequest? TryClassifyVersion(IReadOnlyList<string> positional)
    {
        if (positional.Count != 1)
        {
            return null;
        }

        var token = positional[0];
        if (!IntrinsicVerbs.TryResolve(token, out var verb)
            || !verb.Equals(IntrinsicVerbs.Version, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // `az --version` → text (peer of `az --help`); `az version` → structured JSON.
        var format = token.StartsWith("--", StringComparison.Ordinal)
            ? VersionOutputFormat.Text
            : VersionOutputFormat.Json;
        return new VersionRequest(format);
    }

    private static bool IsHelpFlag(string token)
    {
        foreach (var flag in s_helpFlags)
        {
            if (token.Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
