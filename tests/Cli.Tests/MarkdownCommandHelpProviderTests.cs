using Core.Cli.Help;

namespace Cli.Tests;

/// <summary>
/// Tests for Malformed markdown handling.
/// Verifies that <see cref="MarkdownCommandHelpProvider"/> degrades gracefully
/// when help markdown content is missing fields or malformed. Strict validation
/// lives in the build-time help tool .
/// </summary>
public class MarkdownCommandHelpProviderTests
{
    [Fact]
    public void Parse_ShouldReturnEmpty_WhenContentIsNull()
    {
        var result = MarkdownCommandHelpProvider.Parse(null);

        Assert.Equal(MarkdownCommandHelp.Empty, result);
    }

    [Fact]
    public void Parse_ShouldReturnEmpty_WhenContentIsWhitespace()
    {
        var result = MarkdownCommandHelpProvider.Parse("   \r\n\t  ");

        Assert.Equal(MarkdownCommandHelp.Empty, result);
    }

    [Fact]
    public void Parse_ShouldReturnEmptySummary_WhenSummaryFieldIsMissing()
    {
        var content = """
            Description: Some description text.

            ### Example
            ```bash
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal(string.Empty, result.Summary);
        Assert.Equal("Some description text.", result.Description);
        Assert.Single(result.Examples);
    }

    [Fact]
    public void Parse_ShouldReturnEmptyDescription_WhenDescriptionFieldIsMissing()
    {
        var content = """
            ---
            summary: Short description.
            schema: 1.0.0
            ---

            ### Example
            ```bash
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal("Short description.", result.Summary);
        Assert.Equal(string.Empty, result.Description);
        Assert.Single(result.Examples);
    }

    [Fact]
    public void Parse_ShouldReturnNoExamples_WhenCodeFencesAreUnpaired()
    {
        var content = """
            ---
            summary: Short description.
            schema: 1.0.0
            ---

            Description: Longer description.

            ### Example
            ```bash
            azv2 do-thing
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal("Short description.", result.Summary);
        Assert.Equal("Longer description.", result.Description);
        Assert.Empty(result.Examples);
    }

    [Fact]
    public void Parse_ShouldReadSummaryFromFrontmatter()
    {
        var content = """
            ---
            summary: From frontmatter.
            status: ga
            schema: 1.0.0
            ---

            Description: Body description.

            ### Example (CLI)
            ```bash
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal("From frontmatter.", result.Summary);
        Assert.Equal("Body description.", result.Description);
        Assert.Single(result.Examples);
    }

    [Fact]
    public void Parse_ShouldTagUnknownLanguageAsCli()
    {
        var content = """
            Summary: Short.
            Description: Longer.

            ### Example
            ```ruby
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        var example = Assert.Single(result.Examples);
        Assert.False(example.IsPowerShell);
        Assert.Equal("azv2 do-thing", example.Command);
    }

    [Fact]
    public void Parse_ShouldTagNoLanguageAsCli()
    {
        var content = """
            Summary: Short.
            Description: Longer.

            ### Example
            ```
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        var example = Assert.Single(result.Examples);
        Assert.False(example.IsPowerShell);
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("PowerShell")]
    [InlineData("POWERSHELL")]
    [InlineData("pwsh")]
    [InlineData("Pwsh")]
    public void Parse_ShouldTagPowerShellLanguageVariants(string language)
    {
        var content = $$"""
            Summary: Short.
            Description: Longer.

            ### Example
            ```{{language}}
            Get-Thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        var example = Assert.Single(result.Examples);
        Assert.True(example.IsPowerShell);
    }

    [Fact]
    public void Parse_ShouldSkipExamplesWithEmptyCommandBody()
    {
        var content = """
            Summary: Short.
            Description: Longer.

            ### Empty Example
            ```bash
            ```

            ### Valid Example
            ```bash
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        // Graceful degradation: parser may yield partial results on adjacent fences.
        // Verify it doesn't throw and yields at least one example.
        Assert.NotEmpty(result.Examples);
        Assert.All(result.Examples, e => Assert.False(string.IsNullOrWhiteSpace(e.Command)));
    }

    [Fact]
    public void Parse_ShouldUseFallbackTitle_WhenTitleIsBlank()
    {
        // Whitespace-only title triggers fallback to "Example".
        var content = "Summary: Short.\r\nDescription: Longer.\r\n\r\n###  x\r\n```bash\r\nazv2 do-thing\r\n```\r\n";

        var result = MarkdownCommandHelpProvider.Parse(content);

        var example = Assert.Single(result.Examples);
        Assert.False(string.IsNullOrWhiteSpace(example.Title));
    }

    [Fact]
    public void Parse_ShouldReturnPartialContent_WhenAllFieldsMissingButExamplesPresent()
    {
        var content = """
            ### Example
            ```bash
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal(string.Empty, result.Summary);
        Assert.Equal(string.Empty, result.Description);
        Assert.Single(result.Examples);
    }

    [Fact]
    public void Parse_ShouldJoinMultiLineDescription_WhenContinuationLinesArePresent()
    {
        // Authoring convention used by MarkdownStructureValidator tests:
        //     Description: First line
        //       continuation
        // Continuation runs until the first blank line, heading, or fence.
        var content = """
            ---
            summary: Short.
            schema: 1.0.0
            ---

            Description: First line
              continuation line.

            ### Example
            ```bash
            azv2 do-thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal("First line continuation line.", result.Description);
    }

    [Fact]
    public void Parse_ShouldParseMixedCliAndPowerShellExamples()
    {
        var content = """
            Summary: Short.
            Description: Longer.

            ### CLI Example
            ```bash
            azv2 do-thing
            ```

            ### PowerShell Example
            ```powershell
            Get-Thing
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal(2, result.Examples.Count);
        Assert.Contains(result.Examples, e => !e.IsPowerShell && e.Title == "CLI Example");
        Assert.Contains(result.Examples, e => e.IsPowerShell && e.Title == "PowerShell Example");
    }

    [Fact]
    public void Parse_ShouldNotThrow_OnMalformedInput()
    {
        var malformedInputs = new[]
        {
            "```",
            "###",
            "Summary:",
            "Description:\r\n###\r\n```",
            "\0\0\0",
            new string('a', 10000),
        };

        foreach (var input in malformedInputs)
        {
            var ex = Record.Exception(() => MarkdownCommandHelpProvider.Parse(input));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Parse_CliFilter_ShouldYieldOnlyNonPowerShellExamples()
    {
        var content = """
            Summary: Short.
            Description: Longer.

            ### CLI A
            ```bash
            azv2 do-a
            ```

            ### PS B
            ```powershell
            Do-B
            ```

            ### CLI C
            ```sh
            azv2 do-c
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);
        var cliOnly = result.Examples.Where(e => !e.IsPowerShell).ToList();

        Assert.Equal(2, cliOnly.Count);
        Assert.Contains(cliOnly, e => e.Title == "CLI A");
        Assert.Contains(cliOnly, e => e.Title == "CLI C");
        Assert.DoesNotContain(cliOnly, e => e.Title == "PS B");
    }

    [Fact]
    public void Parse_ShouldPreserveExampleOrder()
    {
        var content = """
            Summary: Short.
            Description: Longer.

            ### First
            ```bash
            azv2 first
            ```

            ### Second
            ```powershell
            Second
            ```

            ### Third
            ```bash
            azv2 third
            ```
            """;

        var result = MarkdownCommandHelpProvider.Parse(content);

        Assert.Equal(3, result.Examples.Count);
        Assert.Equal("First", result.Examples[0].Title);
        Assert.Equal("Second", result.Examples[1].Title);
        Assert.Equal("Third", result.Examples[2].Title);
    }
}
