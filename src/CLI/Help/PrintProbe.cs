// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// Cold-path diagnostic that prints a sample block to stdout, used to eyeball
/// terminal wrapping, encoding pass-through, and column alignment. Runs before
/// DI/dispatch. Not listed in <c>az --help</c>.
/// </summary>
/// <remarks>
/// Activated by either <c>--print-probe</c> (primary) or <c>--meow</c> (alias).
/// Shapes (either flag interchangeable):
/// <list type="bullet">
///   <item><c>az --print-probe</c> — print a random sample.</item>
///   <item><c>az --print-probe --list</c> — print the roster of sample names.</item>
///   <item><c>az --print-probe &lt;name&gt;</c> — print the named sample, or a "not found" hint.</item>
/// </list>
/// </remarks>
internal static class PrintProbe
{
    private const string PrintProbeFlag = "--print-probe";
    private const string MeowFlag = "--meow";
    private const string ListFlag = "--list";

    private static bool IsProbeFlag(string token) =>
        string.Equals(token, PrintProbeFlag, StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, MeowFlag, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns an exit code when <paramref name="args"/> is a probe invocation
    /// (<c>--print-probe</c> or <c>--meow</c>), or <c>null</c> to let the caller
    /// continue normal dispatch.
    /// </summary>
    public static int? TryHandle(string[] args, TextWriter output)
    {
        // Only fires when a probe flag is args[0], mirroring --upload-telemetry / --version.
        if (args.Length == 0 || !IsProbeFlag(args[0]))
        {
            return null;
        }

        // Echo back whichever flag the user typed so hint messages stay consistent
        // with how the invocation was spelled.
        var invokedFlag = args[0];

        // `az <flag> --list` — sorted names, one per line.
        if (args.Length == 2 && string.Equals(args[1], ListFlag, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in Cats.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                output.WriteLine(name);
            }
            return 0;
        }

        // `az <flag>` — random sample.
        if (args.Length == 1)
        {
            output.Write(Cats.Random(Random.Shared));
            return 0;
        }

        // `az <flag> <name>` — named sample.
        if (args.Length == 2)
        {
            if (Cats.TryGet(args[1], out var art))
            {
                output.Write(art);
                return 0;
            }

            output.WriteLine($"No sample named '{args[1]}'. Try 'az {invokedFlag} --list'.");
            return 1;
        }

        // args.Length >= 3 — unknown shape (e.g. `az --meow foo bar`).
        output.WriteLine($"Usage: az {invokedFlag} [<name> | --list]");
        return 1;
    }
}
