using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for the turn-protocol predicates. <see cref="ChatAgent.IsClosingTool"/>
/// decides whether a turn satisfied the act-through-tools contract (gap #6);
/// <see cref="ChatAgent.IsSessionTerminator"/> decides whether a tool ends the
/// session (gap #2). Under the unified-mind model only submit_response terminates,
/// while both propose_plan and submit_response are turn-closing.
/// </summary>
public class ClosingToolPredicateTests
{
    [Theory]
    [InlineData("propose_plan")]
    [InlineData("submit_response")]
    [InlineData("SUBMIT_RESPONSE")]
    public void Closing_tools_are_recognized(string tool)
    {
        Assert.True(ChatAgent.IsClosingTool(tool));
    }

    [Theory]
    [InlineData("get_command")]
    [InlineData("list_commands")]
    [InlineData("find_resources")]
    [InlineData("bash")]
    [InlineData("powershell")]
    [InlineData("")]
    [InlineData(null)]
    public void Non_closing_tools_are_rejected(string? tool)
    {
        Assert.False(ChatAgent.IsClosingTool(tool));
    }

    [Theory]
    [InlineData("submit_response")]
    [InlineData("Submit_Response")]
    public void Only_submit_response_terminates_the_session(string tool)
    {
        Assert.True(ChatAgent.IsSessionTerminator(tool));
    }

    [Theory]
    [InlineData("propose_plan")]
    [InlineData("bash")]
    [InlineData("get_command")]
    [InlineData(null)]
    public void Propose_plan_and_others_do_not_terminate(string? tool)
    {
        // propose_plan is a mid-turn artifact, not a terminator — this is the
        // unified-mind flip (it used to end one-shot sessions).
        Assert.False(ChatAgent.IsSessionTerminator(tool));
    }
}
