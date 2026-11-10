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
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
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

/// <summary>
/// Contract (#1295): only the session token abandons a started y/n read; it ends the prompt with
/// OperationCanceledException, the same signal Ctrl+C gives while thinking. The queue token only
/// stops waiting for a request, so the prompt stays until answered.
/// </summary>
[Collection("Console")]
public class ConsoleApprovalPromptCancelTests
{
    // Console.ReadLine blocks on this reader until the test releases it, like a terminal
    // where the user has not pressed Enter.
    private sealed class BlockingReader : TextReader
    {
        public readonly ManualResetEventSlim Release = new();
        public readonly ManualResetEventSlim Entered = new();
        public string? Line;
        public override string? ReadLine() { Entered.Set(); Release.Wait(); return Line; }
    }

    [Fact]
    public async Task SessionCancel_WhileReadingLine_ThrowsOperationCanceled_AndCancelsTool()
    {
        var prompt = new ConsoleApprovalPrompt();
        var reader = new BlockingReader();
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(reader);
        Console.SetOut(TextWriter.Null);
        try
        {
            using var session = new CancellationTokenSource();
            using var queue = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", session.Token);
            // The pump reads synchronously once an approval is queued, so start it off the test thread.
            var pump = Task.Run(() => prompt.PumpOneAsync(queue.Token, session.Token));
            Assert.True(reader.Entered.Wait(TimeSpan.FromSeconds(10)));

            session.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pump.WaitAsync(TimeSpan.FromSeconds(10)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toolTask);
        }
        finally
        {
            reader.Release.Set();
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task SessionCancel_WhileReadingLine_CompletesRequestNotYetCancelled()
    {
        var prompt = new ConsoleApprovalPrompt();
        var reader = new BlockingReader();
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(reader);
        Console.SetOut(TextWriter.Null);
        try
        {
            using var session = new CancellationTokenSource();
            // The request token has not observed the session cancel yet.
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var pump = Task.Run(() => prompt.PumpOneAsync(session.Token, session.Token));
            Assert.True(reader.Entered.Wait(TimeSpan.FromSeconds(10)));

            session.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pump.WaitAsync(TimeSpan.FromSeconds(10)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toolTask.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            reader.Release.Set();
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task QueueCancel_WhileReadingLine_KeepsReading_AndHonorsAnswer()
    {
        var prompt = new ConsoleApprovalPrompt();
        var reader = new BlockingReader { Line = "y" };
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(reader);
        Console.SetOut(TextWriter.Null);
        try
        {
            using var queue = new CancellationTokenSource();
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var pump = Task.Run(() => prompt.PumpOneAsync(queue.Token, CancellationToken.None));
            Assert.True(reader.Entered.Wait(TimeSpan.FromSeconds(10)));

            queue.Cancel();
            await Task.Delay(200);
            Assert.False(pump.IsCompleted);

            reader.Release.Set();
            var outcome = await pump.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(outcome.Approved);
            Assert.True(await toolTask);
        }
        finally
        {
            reader.Release.Set();
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }
}
