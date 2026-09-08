// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Aggregated result of one or more validator runs. Validators do not throw;
    /// callers (Program.cs / tests) inspect <see cref="HasErrors"/> to decide what to do.
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues = new();

        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public IEnumerable<ValidationIssue> Errors =>
            _issues.Where(i => i.Severity == ValidationSeverity.Error);

        public IEnumerable<ValidationIssue> Warnings =>
            _issues.Where(i => i.Severity == ValidationSeverity.Warning);

        public bool HasErrors => _issues.Any(i => i.Severity == ValidationSeverity.Error);

        public void Add(ValidationIssue issue) => _issues.Add(issue);

        public void AddError(string problemId, string message, string? filePath = null) =>
            _issues.Add(new ValidationIssue(ValidationSeverity.Error, message, filePath, problemId));

        public void AddError(string problemId, string message, string? filePath, int? line) =>
            _issues.Add(new ValidationIssue(ValidationSeverity.Error, message, filePath, problemId, line));

        public void AddWarning(string problemId, string message, string? filePath = null) =>
            _issues.Add(new ValidationIssue(ValidationSeverity.Warning, message, filePath, problemId));

        public void Merge(ValidationReport other)
        {
            foreach (var issue in other._issues)
            {
                _issues.Add(issue);
            }
        }
    }
}
