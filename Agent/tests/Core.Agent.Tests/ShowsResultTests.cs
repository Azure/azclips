using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Direct assertions for <see cref="AgentEventStyle.ShowsResult"/> — the predicate
/// that decides whether a successful tool's result is printed inline. A snapshot
/// test can't guard this: all the snapshot fixtures use a shell tool (the show
/// path), so the hide path (get_command / find_* / propose_plan) has no coverage,
/// and a snapshot would pass even with a fabricated tool name. These tests pin the
/// real tool names to their expected booleans, so flipping the rule — or adding a
/// chatty tool to the show set — fails immediately.
/// </summary>
public class ShowsResultTests
{
    [Theory]
    [InlineData("bash")]
    [InlineData("powershell")]
    [InlineData("PowerShell")]   // case-insensitive
    public void Shell_tools_show_their_result(string toolName)
    {
        Assert.True(AgentEventStyle.ShowsResult(toolName));
    }

    [Theory]
    [InlineData("get_command")]
    [InlineData("list_commands")]
    [InlineData("find_resources")]
    [InlineData("find_available_locations")]
    [InlineData("propose_plan")]
    [InlineData("submit_response")]
    [InlineData("get_resource_details")]
    public void Scaffolding_tools_hide_their_result(string toolName)
    {
        Assert.False(AgentEventStyle.ShowsResult(toolName));
    }
}
