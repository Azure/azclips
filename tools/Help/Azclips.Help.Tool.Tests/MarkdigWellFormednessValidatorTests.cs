// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azclips.Help.Tool.Validation;

namespace Azclips.Help.Tool.Tests;

public class MarkdigWellFormednessValidatorTests
{
    [Fact]
    public void CleanContent_NoIssues()
    {
        var content = """
            ---
            summary: Create a VM.
            status: ga
            schema: 1.0.0
            ---

            Description: Provision a virtual machine. The `--size` flag picks a SKU.

            ### Basic

            ```bash
            az vm create --name myvm
            ```
            """;

        var report = ValidateContent(content);

        Assert.Empty(report.Errors);
    }

    [Fact]
    public void PlaceholderInAngleBrackets_ReportsSuspiciousHtmlTag()
    {
        var content = """
            ---
            summary: Create a VM named <vm-name>.
            status: ga
            schema: 1.0.0
            ---

            Description: Sample.
            """;

        var report = ValidateContent(content);

        var error = Assert.Single(report.Errors);
        Assert.Equal(ProblemIds.MD_SuspiciousHtmlTag, error.ProblemId);
        Assert.Contains("vm-name", error.Message);
    }

    [Fact]
    public void RealHtmlBrTag_IsAllowed()
    {
        var content = """
            ---
            summary: Line one<br>line two.
            status: ga
            schema: 1.0.0
            ---

            Description: Sample.
            """;

        var report = ValidateContent(content);

        Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.MD_SuspiciousHtmlTag);
    }

    [Fact]
    public void UnclosedInlineCode_ReportsUnbalancedBackticks()
    {
        var content = """
            ---
            summary: Sample.
            status: ga
            schema: 1.0.0
            ---

            Description: Set `--size to Standard_D2s_v5 for a small VM.
            """;

        var report = ValidateContent(content);

        var error = Assert.Single(report.Errors);
        Assert.Equal(ProblemIds.MD_UnbalancedBackticks, error.ProblemId);
    }

    [Fact]
    public void UnbalancedBackticks_MessageIncludesExcerptAroundBacktick()
    {
        var content = """
            ---
            summary: Sample.
            status: ga
            schema: 1.0.0
            ---

            Description: Configure `--size to Standard_D2s_v5 for a small VM.
            """;

        var report = ValidateContent(content);

        var error = Assert.Single(report.Errors);
        Assert.Contains("near:", error.Message);
        Assert.Contains("`--size", error.Message);
    }

    [Fact]
    public void UnbalancedBackticks_PopulatesLine()
    {
        // Line 7 is the "Description:" paragraph in the content below.
        var content = """
            ---
            summary: Sample.
            status: ga
            schema: 1.0.0
            ---

            Description: Set `--size to Standard_D2s_v5 for a small VM.
            """;

        var report = ValidateContent(content);

        var error = Assert.Single(report.Errors);
        Assert.Equal(7, error.Line);
    }

    [Fact]
    public void BalancedInlineCode_NoIssues()
    {
        var content = """
            ---
            summary: Sample.
            status: ga
            schema: 1.0.0
            ---

            Description: Set `--size` to `Standard_D2s_v5` for a small VM.
            """;

        var report = ValidateContent(content);

        Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.MD_UnbalancedBackticks);
    }

    [Fact]
    public void FencedCodeBlock_ContentIsIgnored()
    {
        // Fenced code content is verbatim — angle brackets and unpaired backticks
        // inside a fence must not fire the prose rules.
        var content = """
            ---
            summary: Sample.
            status: ga
            schema: 1.0.0
            ---

            Description: Example follows.

            ```bash
            az vm create --tags name=<myvm> `
                --admin-password 'P@ss'
            ```
            """;

        var report = ValidateContent(content);

        Assert.Empty(report.Errors);
    }

    [Fact]
    public void MultipleIssues_AllReported()
    {
        var content = """
            ---
            summary: Create a VM named <vm-name>.
            status: ga
            schema: 1.0.0
            ---

            Description: Set `--size to Standard_D2s_v5.
            """;

        var report = ValidateContent(content);

        Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.MD_SuspiciousHtmlTag);
        Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.MD_UnbalancedBackticks);
    }

    private static ValidationReport ValidateContent(string content)
    {
        var report = new ValidationReport();
        MarkdigWellFormednessValidator.ValidateContent(content, "test.md", report);
        return report;
    }
}
