using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

// ConsoleApprovalPromptTests and PlanGateTests both swap the process-global
// Console.In/Out (Console.SetIn/SetOut) to drive the approval prompt's
// Console.ReadLine(). xUnit runs separate test classes in parallel, so without a
// shared collection one class can swap Console.In out from under the other's
// ReadLine — yielding EOF (null feedback) intermittently in CI. This collection
// serializes them; DisableParallelization keeps anything else off Console while
// they run.
[CollectionDefinition("Console", DisableParallelization = true)]
public class ConsoleCollection { }

/// <summary>
/// Contract: y/yes → approve; n/no/empty → reject; anything else → reject with
/// the typed text returned as FeedbackPrompt for the next chat turn.
/// </summary>
[Collection("Console")]
public class ConsoleApprovalPromptTests
{
    private static async Task<(ConsoleApprovalPrompt.ApprovalOutcome outcome, bool toolApproved)>
        DriveAsync(string userInput)
    {
        var prompt = new ConsoleApprovalPrompt();
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(new StringReader(userInput + Environment.NewLine));
        Console.SetOut(TextWriter.Null);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "azv2 group delete --yes", CancellationToken.None);
            var outcome = await prompt.PumpOneAsync(CancellationToken.None);
            var approved = await toolTask;
            return (outcome, approved);
        }
        finally
        {
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }

    [Theory]
    [InlineData("y")]
    [InlineData("Y")]
    [InlineData("yes")]
    [InlineData("YES")]
    [InlineData("  yes  ")]
    public async Task Yes_Variants_ReturnYes_AndApproveTool(string input)
    {
        var (outcome, approved) = await DriveAsync(input);
        Assert.True(outcome.Approved);
        Assert.Null(outcome.FeedbackPrompt);
        Assert.True(approved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    [InlineData("N")]
    [InlineData("no")]
    [InlineData("NO")]
    public async Task No_Variants_ReturnNo_AndRejectTool(string input)
    {
        var (outcome, approved) = await DriveAsync(input);
        Assert.False(outcome.Approved);
        Assert.Null(outcome.FeedbackPrompt);
        Assert.False(approved);
    }

    [Theory]
    [InlineData("use az instead",                  "use az instead")]
    [InlineData("  delete the other rg instead  ", "delete the other rg instead")]
    [InlineData("nope, use --dry-run",             "nope, use --dry-run")]
    public async Task FreeText_ReturnsRedirect_RejectsTool_AndCarriesFeedback(string input, string expectedFeedback)
    {
        var (outcome, approved) = await DriveAsync(input);
        Assert.False(outcome.Approved);
        Assert.Equal(expectedFeedback, outcome.FeedbackPrompt);
        Assert.False(approved);
    }
}
