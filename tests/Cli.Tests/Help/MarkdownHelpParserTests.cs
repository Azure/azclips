// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;

namespace Cli.Tests;

public class MarkdownHelpParserTests
{
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
