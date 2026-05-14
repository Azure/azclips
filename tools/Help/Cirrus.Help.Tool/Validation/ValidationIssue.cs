// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Cirrus.Help.Tool.Validation
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
    public sealed record ValidationIssue(
        ValidationSeverity Severity,
        string Message,
        string? FilePath = null)
    {
        public override string ToString()
        {
            var prefix = Severity == ValidationSeverity.Error ? "error" : "warning";
            return FilePath is null
                ? $"{prefix}: {Message}"
                : $"{prefix}: {FilePath}: {Message}";
        }
    }
}
