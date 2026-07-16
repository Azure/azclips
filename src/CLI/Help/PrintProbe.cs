// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// Cold-path diagnostic that prints a known-good sample block to stdout,
/// used to eyeball terminal wrapping, encoding pass-through, and column
/// alignment when triaging render issues. Routes purely on argv, does no
/// DI resolution, and exits before <see cref="Core.Cli.Program"/> touches
/// the extension dispatcher. Intentionally NOT listed in <c>az --help</c>.
/// </summary>
/// <remarks>
/// Supported shapes:
/// <list type="bullet">
///   <item><c>az --meow</c> — print a random sample.</item>
///   <item><c>az --meow --list</c> — print the roster of sample names.</item>
///   <item><c>az --meow &lt;name&gt;</c> — print the named sample, or a "not found" hint.</item>
/// </list>
/// The flag name is retained for backwards compatibility with the initial
/// diagnostic surface; the sample corpus is authored by contributors
/// (see <see cref="Cats"/>).
/// </remarks>
internal static class PrintProbe
{
    private const string MeowFlag = "--meow";
    private const string ListFlag = "--list";

    /// <summary>
    /// Returns an exit code when <paramref name="args"/> is a <c>--meow</c> invocation,
    /// or <c>null</c> to let the caller continue normal dispatch.
    /// </summary>
    public static int? TryHandle(string[] args, TextWriter output)
    {
        // The easter egg fires only when --meow is the FIRST positional token.
        // This keeps it out of the way of real command lines like `az config set --meow ...`
        // (hypothetical), and matches how other host-level flags (--upload-telemetry,
        // --version) are recognized in the leading slot.
        if (args.Length == 0 || !string.Equals(args[0], MeowFlag, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // `az --meow --list` — print names, one per line, sorted for stable output.
        if (args.Length == 2 && string.Equals(args[1], ListFlag, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in Cats.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                output.WriteLine(name);
            }
            return 0;
        }

        // `az --meow` — random cat.
        if (args.Length == 1)
        {
            output.Write(Cats.Random(Random.Shared));
            return 0;
        }

        // `az --meow <name>` — named cat.
        if (args.Length == 2)
        {
            if (Cats.TryGet(args[1], out var art))
            {
                output.Write(art);
                return 0;
            }

            // Unknown name: nudge toward --list without leaking the fact that it's
            // an easter egg in a scary error tone.
            output.WriteLine($"no cat named '{args[1]}'. try `az --meow --list`.");
            return 1;
        }

        // Extra tokens: keep it simple — same unknown-shape message.
        output.WriteLine("usage: az --meow [<name> | --list]");
        return 1;
    }
}
