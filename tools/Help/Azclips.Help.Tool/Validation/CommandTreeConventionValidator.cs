// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Parser;

namespace Azclips.Help.Tool.Validation;

public sealed class CommandTreeConventionValidator
{
    public ValidationReport Validate(IReadOnlyDictionary<string, IExtensionIndex> extensions)
    {
        var report = new ValidationReport();
        if (extensions.Count == 0)
        {
            report.AddError(ProblemIds.Tool_NoExtensionsFound,
                "No extension indexes were discovered. Build the extensions and check --artifacts-root before validating commands.");
            return report;
        }

        var reserved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var flag in KnownGlobalFlags.All)
        {
            foreach (var name in flag.Aliases.Prepend(flag.Name))
                reserved.TryAdd(name.TrimStart('-'), name);
        }

        var commands = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (extension, index) in extensions)
        {
            // Include registered commands even for indexes using the back-compat
            // empty GetCommandKeys default, plus any additional routed alias keys.
            foreach (var key in index.GetCommandTable(ClientType.CLI).Keys
                         .Concat(index.GetCommandKeys(ClientType.CLI))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = HelpCommandKey.Normalize(string.Join(' ',
                    key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
                if (path.Length == 0)
                {
                    report.AddError(ProblemIds.CLI_InvalidCommandPath,
                        $"Extension '{extension}' exposes an empty CLI command path. Register a full command name including its verb.");
                    continue;
                }

                if (!commands.TryGetValue(path, out var owners))
                {
                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    commands.Add(path, owners);
                }
                owners.Add(extension);
            }
        }

        foreach (var (path, owners) in commands.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var segments = path.Split(' ');
            foreach (var segment in segments.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (reserved.TryGetValue(segment.TrimStart('-'), out var flag))
                {
                    report.AddError(ProblemIds.CLI_ReservedGlobalArgument,
                        $"CLI command 'az {path}' from {FormatOwners(owners)} uses reserved segment '{segment}' from global argument '{flag}'. Rename this command/group segment; global argument names and aliases are not command names.");
                }
            }

            for (var length = 1; length < segments.Length; length++)
            {
                var prefix = string.Join(' ', segments, 0, length);
                if (commands.TryGetValue(prefix, out var parentOwners))
                {
                    report.AddError(ProblemIds.CLI_CommandGroupCollision,
                        $"Executable CLI command 'az {prefix}' from {FormatOwners(parentOwners)} is also a group containing 'az {path}' from {FormatOwners(owners)}. Move the parent action beneath a group-only path.");
                }
            }
        }

        return report;
    }

    private static string FormatOwners(IEnumerable<string> owners) =>
        string.Join(", ", owners.Order(StringComparer.OrdinalIgnoreCase).Select(owner => $"extension '{owner}'"));
}
