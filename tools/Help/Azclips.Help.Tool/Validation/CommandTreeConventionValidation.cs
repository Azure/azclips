// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Validation;

namespace Azclips.Help.Tool.Validation;

internal static class CommandTreeConventionValidation
{
    public static ValidationReport Validate(IReadOnlyDictionary<string, IExtensionIndex> extensions)
    {
        var report = new ValidationReport();
        if (extensions.Count == 0)
        {
            report.AddError(ProblemIds.Tool_NoExtensionsFound,
                "No extension indexes were discovered. Build the extensions and check --artifacts-root before validating commands.");
            return report;
        }

        foreach (var error in new CommandTreeConventionValidator().Validate(extensions))
        {
            var problemId = error.Kind switch
            {
                CommandTreeConventionErrorKind.CommandGroupCollision => ProblemIds.CLI_CommandGroupCollision,
                CommandTreeConventionErrorKind.ReservedGlobalArgument => ProblemIds.CLI_ReservedGlobalArgument,
                CommandTreeConventionErrorKind.InvalidCommandPath => ProblemIds.CLI_InvalidCommandPath,
                _ => throw new ArgumentOutOfRangeException(nameof(error.Kind), error.Kind, "Unknown CLI command convention error."),
            };
            report.AddError(problemId, error.Message);
        }
        return report;
    }
}
