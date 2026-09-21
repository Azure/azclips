// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;
using Core.Cli.Parser;

namespace Core.Cli.Recovery;

internal sealed record TroubleshootRequest(string? Id, bool List)
{
    internal static TroubleshootRequest Parse(GlobalArgs args)
    {
        if (args.Positional.Count == 0 ||
            !args.Positional[0].Equals(IntrinsicVerbs.Troubleshoot, StringComparison.OrdinalIgnoreCase))
        {
            throw AzclipsErrorException.UsageError("Expected az troubleshoot.");
        }

        string? id = null;
        var list = false;
        for (var i = 1; i < args.Positional.Count; i++)
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
            else if (token.Equals("--id", StringComparison.OrdinalIgnoreCase) &&
                id is null && i + 1 < args.Positional.Count)
            {
                id = args.Positional[++i];
            }
            else
            {
                throw AzclipsErrorException.UsageError("Use az troubleshoot [--id <failure-id>] or az troubleshoot --list.");
            }
        }

        if (list && id is not null)
            throw AzclipsErrorException.UsageError("--list and --id cannot be used together.");
        if (id is not null && (!Guid.TryParseExact(id, "N", out var parsed) || parsed == Guid.Empty))
            throw AzclipsErrorException.UsageError("--id must be the full failure ID printed by az or az troubleshoot --list.");
        if (!list && (args.Output is not null || args.Query is not null))
            throw AzclipsErrorException.UsageError("AI diagnosis uses the one-shot AI help display. --output and --query are supported with --list only.");

        return new(id?.ToLowerInvariant(), list);
    }
}
