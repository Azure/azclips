// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Help;
using Core.Cli.Parser;

namespace Core.Cli.Recovery;

internal sealed record TroubleshootRequest(string? Id, bool List, bool Show = false, string? Style = null)
{
    internal bool IsLocalView => List || Show;

    internal static TroubleshootRequest Parse(GlobalArgs args)
    {
        if (args.Positional.Count == 0 ||
            !args.Positional[0].Equals(IntrinsicVerbs.Troubleshoot, StringComparison.OrdinalIgnoreCase))
        {
            throw AzclipsErrorException.UsageError("Expected az troubleshoot.");
        }

        string? id = null;
        string? style = null;
        var list = false;
        var show = args.Positional.Count > 1 &&
            args.Positional[1].Equals("show", StringComparison.OrdinalIgnoreCase);
        for (var i = show ? 2 : 1; i < args.Positional.Count; i++)
        {
            var token = args.Positional[i];
            if (token.Equals("--list", StringComparison.OrdinalIgnoreCase) && !list)
            {
                list = true;
            }
            else if (token.StartsWith("--id=", StringComparison.OrdinalIgnoreCase) && id is null)
            {
                id = token["--id=".Length..];
            }
            else if (token.Equals("--id", StringComparison.OrdinalIgnoreCase) && id is null)
            {
                id = ReadValue(args.Positional, ref i);
            }
            else if (token.StartsWith("--style=", StringComparison.OrdinalIgnoreCase) && style is null)
            {
                style = token["--style=".Length..];
            }
            else if (token.Equals("--style", StringComparison.OrdinalIgnoreCase) && style is null)
            {
                style = ReadValue(args.Positional, ref i);
            }
            else
            {
                throw AzclipsErrorException.UsageError(
                    "Use az troubleshoot [--id <failure-id>] [--style oneshot|interactive], az troubleshoot --list, or az troubleshoot show --id <failure-id>. Duplicate and unknown arguments are not supported.");
            }
        }

        if (list && (id is not null || show))
            throw AzclipsErrorException.UsageError("--list cannot be combined with --id or show.");
        if (show && id is null)
            throw AzclipsErrorException.UsageError("Use az troubleshoot show --id <failure-id> to view one saved failure.");
        if (id is not null && !FailureId.TryParse(id, out _))
            throw AzclipsErrorException.UsageError("--id must be the full failure ID printed by az or az troubleshoot --list.");
        if (style is not null && (list || show))
            throw AzclipsErrorException.UsageError("--style applies to AI analysis only; it cannot be combined with --list or show.");
        style = style?.ToLowerInvariant();
        if (style is not null and not ("oneshot" or "interactive"))
            throw AzclipsErrorException.UsageError("--style must be oneshot or interactive.");
        if (!list && !show && (args.Output is not null || args.Query is not null))
            throw AzclipsErrorException.UsageError(
                "AI diagnosis uses the existing AI help display, not a machine-readable plan format. --output and --query are supported with --list or show only.");

        return new(id, list, show, style);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith('-'))
            throw AzclipsErrorException.UsageError($"{args[index]} requires a value.");

        return args[++index];
    }
}
