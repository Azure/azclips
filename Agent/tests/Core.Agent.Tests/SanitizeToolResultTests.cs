using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="AgentEventStyle.SanitizeToolResult"/> — the pure
/// string cleaner that strips terminal noise (ANSI colour codes, \r/\b overwrite
/// runs), de-dupes adjacent lines, and truncates very long output. Pure in/out,
/// so it's cheap to pin against the spinner-noise scenarios it was written for.
/// </summary>
public class SanitizeToolResultTests
{
    [Fact]
    public void Strips_ansi_color_codes()
    {
        var raw = "\x1b[32mRunning\x1b[0m done";
        var cleaned = AgentEventStyle.SanitizeToolResult(raw);
        Assert.DoesNotContain('\x1b', cleaned);
        Assert.Contains("done", cleaned);
    }

    [Fact]
    public void Removes_carriage_return_overwrite_runs()
    {
        // az CLI spinners emit "\rRunning.." repeatedly on one line. The overwrite
        // run (\r + the text after it) is dropped, leaving the final settled text.
        var raw = "first line\nRunning..\rRunning..\rDone";
        var cleaned = AgentEventStyle.SanitizeToolResult(raw);
        Assert.DoesNotContain('\r', cleaned);
    }

    [Fact]
    public void Dedupes_adjacent_identical_lines()
    {
        var raw = "Running\nRunning\nRunning\nDone";
        var cleaned = AgentEventStyle.SanitizeToolResult(raw);
        var lines = cleaned.Split('\n');
        Assert.Equal(new[] { "Running", "Done" }, lines);
    }

    [Fact]
    public void Keeps_non_adjacent_duplicates()
    {
        // Only *adjacent* duplicates collapse; A/B/A must stay A/B/A.
        var raw = "A\nB\nA";
        var cleaned = AgentEventStyle.SanitizeToolResult(raw);
        Assert.Equal(new[] { "A", "B", "A" }, cleaned.Split('\n'));
    }

    [Fact]
    public void Truncates_long_output_to_tail_with_marker()
    {
        // > 4000 chars → keep only the last 20 lines, prefixed with a marker.
        var raw = string.Join('\n', Enumerable.Range(1, 5000).Select(i => $"line{i}"));
        var cleaned = AgentEventStyle.SanitizeToolResult(raw);

        Assert.StartsWith("[output truncated", cleaned);
        Assert.Contains("line5000", cleaned);       // tail kept
        Assert.DoesNotContain("line1\n", cleaned);  // head dropped
        // marker line + 20 tail lines
        Assert.Equal(21, cleaned.Split('\n').Length);
    }

    [Fact]
    public void Empty_input_passes_through()
    {
        Assert.Equal("", AgentEventStyle.SanitizeToolResult(""));
    }
}
