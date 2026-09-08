// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Severity of a help validation issue.
    /// </summary>
    public enum ValidationSeverity
    {
        Warning,
        Error,
    }

    /// <summary>
    /// A single issue raised by a help validator.
    /// </summary>
    /// <param name="Severity">Error or Warning. Only errors fail the build.</param>
    /// <param name="Message">Human-readable description, suitable for printing to stdout/stderr.</param>
    /// <param name="FilePath">Optional repo-relative path of the file the issue relates to.</param>
    /// <param name="ProblemId">
    /// Stable identifier for the rule that produced the issue (e.g. <c>CLI_L1_UnknownGroup</c>,
    /// <c>MD_MissingSummary</c>). Used to group the final summary and, eventually, to baseline
    /// individual rules in CI. See <see cref="ProblemIds"/> for the canonical vocabulary.
    /// </param>
    /// <param name="Line">Optional 1-based line number, when the validator can pinpoint one. Consumed by the GitHub Actions annotation emitter to render inline squiggles on the PR diff.</param>
    public sealed record ValidationIssue(
        ValidationSeverity Severity,
        string Message,
        string? FilePath = null,
        string? ProblemId = null,
        int? Line = null)
    {
        public override string ToString()
        {
            var prefix = Severity == ValidationSeverity.Error ? "error" : "warning";
            var id = ProblemId is null ? string.Empty : $" [{ProblemId}]";
            return FilePath is null
                ? $"{prefix}{id}: {Message}"
                : $"{prefix}{id}: {FilePath}: {Message}";
        }
    }
}
