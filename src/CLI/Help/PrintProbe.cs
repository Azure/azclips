// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// Cold-path diagnostic that prints a sample block to stdout, used to eyeball
/// terminal wrapping, encoding pass-through, and column alignment. Runs before
/// DI/dispatch. Not listed in <c>az --help</c>.
/// </summary>
/// <remarks>
/// Shapes:
/// <list type="bullet">
///   <item><c>az --meow</c> — print a random sample.</item>
///   <item><c>az --meow --list</c> — print the roster of sample names.</item>
///   <item><c>az --meow &lt;name&gt;</c> — print the named sample, or a "not found" hint.</item>
/// </list>
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
        // Only fires when --meow is args[0], mirroring --upload-telemetry / --version.
        if (args.Length == 0 || !string.Equals(args[0], MeowFlag, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // `az --meow --list` — sorted names, one per line.
        if (args.Length == 2 && string.Equals(args[1], ListFlag, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in Cats.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                output.WriteLine(name);
            }
            return 0;
        }

        // `az --meow` — random sample.
        if (args.Length == 1)
        {
            output.Write(Cats.Random(Random.Shared));
            return 0;
        }

        // `az --meow <name>` — named sample.
        if (args.Length == 2)
        {
            if (Cats.TryGet(args[1], out var art))
            {
                output.Write(art);
                return 0;
            }

            output.WriteLine($"no sample named '{args[1]}'. try `az --meow --list`.");
            return 1;
        }

        output.WriteLine("usage: az --meow [<name> | --list]");
        return 1;
    }
}
