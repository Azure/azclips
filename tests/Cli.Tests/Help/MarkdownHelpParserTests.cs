// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;

namespace Cli.Tests;

public class MarkdownHelpParserTests
{
    [Theory]
    [InlineData("\n", "")]
    [InlineData("\r\n", "")]
    [InlineData("\n", "### Example")]
    [InlineData("\r\n", "### Example")]
    [InlineData("\n", "```bash")]
    [InlineData("\r\n", "```bash")]
    public void GetField_PreservesParagraphsAndStopsAtHeadingOrFence(string newline, string boundary)
    {
        var content = string.Join(newline,
            "Description: First paragraph",
            "  continued on another line.",
            "",
            " \t ",
            "Second paragraph",
            "continued too.",
            "",
            boundary);

        var description = MarkdownHelpParser.GetField(content, "Description");

        Assert.Equal("First paragraph continued on another line.\n\nSecond paragraph continued too.", description);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void EnumerateExamples_NormalizesMultilineCommandsWithoutChangingContent(string newline)
    {
        var content = string.Join(newline,
            "### Switch profile",
            "",
            "```bash",
            "az login --out-profile dev",
            "",
            "  az profile set --name prod",
            "az group list",
            "```");

        var example = Assert.Single(MarkdownHelpParser.EnumerateExamples(content));

        Assert.Equal("Switch profile", example.Title);
        Assert.Equal("bash", example.Language);
        Assert.Equal("az login --out-profile dev\n\n  az profile set --name prod\naz group list", example.Command);
    }
}
