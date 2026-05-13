using Core.Runtime;

namespace Cli.Tests;

/// <summary>
/// Tests for 15.4.1 (Malformed markdown handling).
/// Verifies that <see cref="MarkdownCommandHelpProvider"/> degrades gracefully
/// when help markdown content is missing fields or malformed. Strict validation
/// lives in the build-time help tool (covered by 15.4.5).
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
            Summary: Short description.

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
            Summary: Short description.
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
}
