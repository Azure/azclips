using Core.Agent.AgentMode;
using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for how one-shot recovery starts and ends: the first prompt shows the
/// failed command as the user typed it (with <c>az</c> in the CLI flavor), and the
/// plan tools end the agent turn only when asked to (one-shot).
/// </summary>
public class OneShotRecoveryTests
{
    [Fact]
    public void First_prompt_adds_az_for_the_cli_flavor() =>
        Assert.Equal(
            "I tried `az rest --method fetch --uri https://example` but it failed:\nbad method\nHelp me fix it.",
            AgentModeEntry.BuildFirstPrompt("rest --method fetch --uri https://example", "bad method", "azure cli"));

    [Fact]
    public void First_prompt_keeps_an_existing_az() =>
        Assert.Equal(
            "I tried `az vm show` but it failed. Help me fix it.",
            AgentModeEntry.BuildFirstPrompt("az vm show", errorText: null, "azure cli"));

    [Fact]
    public void First_prompt_leaves_powershell_cmdlets_unchanged() =>
        Assert.Equal(
            "I tried `New-AzVM -Name x` but it failed. Help me fix it.",
            AgentModeEntry.BuildFirstPrompt("New-AzVM -Name x", errorText: null, "azure powershell"));

    [Fact]
    public void Plan_and_submit_tools_end_the_turn_only_when_requested()
    {
        Assert.True(IsTerminal(new ProposePlanTool(endsTurn: true).ProposePlan));
        Assert.True(IsTerminal(new SubmitResponseTool(endsTurn: true).SubmitResponse));
        Assert.False(IsTerminal(new ProposePlanTool().ProposePlan));
        Assert.False(IsTerminal(new SubmitResponseTool().SubmitResponse));
    }

    [Fact]
    public void Terminal_tools_keep_their_name_and_parameters()
    {
        var terminal = new ProposePlanTool(endsTurn: true).ProposePlan;
        var regular = new ProposePlanTool().ProposePlan;

        Assert.Equal("propose_plan", terminal.Name);
        Assert.Equal(regular.Description, terminal.Description);
        Assert.Equal(regular.JsonSchema.GetRawText(), terminal.JsonSchema.GetRawText());
    }

    // CopilotToolOptions.IsTerminal is documented to add "is_terminal": true to AdditionalProperties.
    private static bool IsTerminal(Microsoft.Extensions.AI.AIFunction function) =>
        function.AdditionalProperties.TryGetValue("is_terminal", out var value) && value is true;
}
