using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ConsoleApprovalPrompt.MarkdownToText"/> — the
/// plan-markdown → plain-console converter. Pins the deliberate UX decision to
/// STRIP backticks from placeholders (the angle brackets already signal "fill
/// me in", so backticks are noise), plus heading/bold handling and the
/// no-trailing-blank-line rule.
/// </summary>
public class MarkdownToTextTests
{
    [Fact]
    public void Strips_backticks_from_placeholders()
    {
        // Decision (wzl): `<your-admin-password>` renders without backticks.
        var lines = ConsoleApprovalPrompt.MarkdownToText("replace `<your-admin-password>` now");
        Assert.Single(lines);
        Assert.DoesNotContain("`", lines[0]);
        Assert.Contains("<your-admin-password>", lines[0]);
    }

    [Fact]
    public void Strips_bold_markers()
    {
        var lines = ConsoleApprovalPrompt.MarkdownToText("this is **important**");
        Assert.Equal("this is important", lines[0]);
    }

    [Fact]
    public void Converts_heading_to_label_with_blank_line_before()
    {
        var lines = ConsoleApprovalPrompt.MarkdownToText("intro\n## Steps");
        // "intro", then a blank separator, then "Steps:"
        Assert.Equal(new[] { "intro", "", "Steps:" }, lines);
    }

    [Fact]
    public void Drops_trailing_blank_line_from_trailing_newline()
    {
        // planMd commonly ends with "\n"; that must not produce a trailing blank.
        var lines = ConsoleApprovalPrompt.MarkdownToText("only line\n");
        Assert.Equal(new[] { "only line" }, lines);
    }
}
