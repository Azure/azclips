using Core.Agent.AgentMode;
using Core.Agent.Hosting;
using Microsoft.Extensions.Time.Testing;
using Tests.Common.Utilities;
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

    // A watcher past KeyAvailable must not block Ctrl+C once the approval read is abandoned.
    [Fact]
    public async Task Watcher_PastGate_DoesNotBlockCtrlC_WhenApprovalReadIsAbandoned()
    {
        var keys = new ScriptedConsoleKeys();
        keys.Push(ScriptedConsoleKeys.Char('x'));
        using var hookEntered = new ManualResetEventSlim();
        using var hookRelease = new ManualResetEventSlim();
        var first = 0;
        keys.AfterKeyAvailable = _ =>
        {
            if (Interlocked.Exchange(ref first, 1) == 0) { hookEntered.Set(); hookRelease.Wait(); }
        };
        var reader = new LockingBlockingReader(keys.StdinLock);
        var time = new SignalingFakeTimeProvider();
        using var session = new CancellationTokenSource();
        var ctrlC = new CtrlCController(session, time);
        var agent = new FakeTurnAgent();
        var approval = new ConsoleApprovalPrompt();
        using var console = new ConsoleSwap(reader);
        try
        {
            var watcher = TurnPump.Watcher(agent, keys, time, ctrlC.OnCtrlC);
            var pump = TurnPump.RunAsync(agent, approval, watcher, interactive: true, () => ctrlC.SessionCancelled, session.Token);
            Assert.True(hookEntered.Wait(TimeSpan.FromSeconds(60)));
            var tool = approval.RequestRewriteAsync("powershell", "az group delete", session.Token);
            // The pump must stop the watcher before it reads; it waits for the hook, so release it from here.
            var released = Task.Run(() => { hookRelease.Set(); });
            Assert.True(reader.Entered.Wait(TimeSpan.FromSeconds(60)));

            ctrlC.OnCtrlC();
            await time.WaitForTimerAsync(CtrlCController.ForcedExitDelay, TimeSpan.FromSeconds(60));
            time.Advance(CtrlCController.ForcedExitDelay);

            Assert.Equal(130, await ctrlC.ForcedExit.WaitAsync(TimeSpan.FromSeconds(60)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pump.WaitAsync(TimeSpan.FromSeconds(60)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.WaitAsync(TimeSpan.FromSeconds(60)));
            await released.WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            hookRelease.Set();
            reader.Release.Set();
        }
    }

    // Esc read before an approval opens rejects that approval without a prompt.
    [Fact]
    public async Task EscBeforeApprovalOpens_RejectsApproval_AndSkipsPrompt()
    {
        var keys = new ScriptedConsoleKeys();
        keys.Push(ScriptedConsoleKeys.Esc);
        var reader = new LockingBlockingReader();
        using var session = new CancellationTokenSource();
        var agent = new FakeTurnAgent { HoldAbort = true };
        var approval = new ConsoleApprovalPrompt();
        using var console = new ConsoleSwap(reader);
        try
        {
            var watcher = TurnPump.Watcher(agent, keys, new FakeTimeProvider());
            var pump = TurnPump.RunAsync(agent, approval, watcher, interactive: true, () => session.IsCancellationRequested, session.Token);
            await agent.AbortEntered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var tool = approval.RequestRewriteAsync("powershell", "az group delete", CancellationToken.None);
            agent.AbortRelease.SetResult();

            var result = await pump.WaitAsync(TimeSpan.FromSeconds(60));
            Assert.True(result.UserAborted);
            Assert.False(await tool.WaitAsync(TimeSpan.FromSeconds(60)));
            Assert.False(reader.Entered.IsSet);
        }
        finally
        {
            agent.AbortRelease.TrySetResult();
            reader.Release.Set();
        }
    }

    // A key seen by KeyAvailable after the watcher was cancelled is not read.
    [Fact]
    public async Task TurnEnd_KeyTypedAfterStop_IsNotConsumedByWatcher()
    {
        var keys = new ScriptedConsoleKeys();
        keys.Push(ScriptedConsoleKeys.Esc);
        using var stop = new CancellationTokenSource();
        keys.AfterKeyAvailable = available => { if (available) stop.Cancel(); };
        var aborts = 0;
        var watcher = new AbortKeyWatcher(_ => { aborts++; return Task.CompletedTask; }, null, TurnPump.Poll, keys, new FakeTimeProvider());

        var aborted = await watcher.RunAsync(stop.Token, () => true).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.False(aborted);
        Assert.Equal(0, aborts);
    }

    // Approvals still queued when the turn ends are resolved.
    [Fact]
    public async Task TurnEnd_ResolvesEveryQueuedApproval()
    {
        var agent = new FakeTurnAgent();
        var approval = new ConsoleApprovalPrompt();
        var first = approval.RequestRewriteAsync("powershell", "a", CancellationToken.None);
        var second = approval.RequestRewriteAsync("powershell", "b", CancellationToken.None);
        agent.Send.SetResult();
        var reader = new LockingBlockingReader();
        using var console = new ConsoleSwap(reader);
        using var session = new CancellationTokenSource();
        try
        {
            var watcher = TurnPump.Watcher(agent, new ScriptedConsoleKeys(), new FakeTimeProvider());

            await TurnPump.RunAsync(agent, approval, watcher, interactive: true, () => false, session.Token)
                .WaitAsync(TimeSpan.FromSeconds(60));

            Assert.False(await first.WaitAsync(TimeSpan.FromSeconds(60)));
            Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(60)));
        }
        finally
        {
            reader.Release.Set();
        }
    }

    private static ReadConsoleChunk Chunks(params Func<(string Text, int Error)>[] chunks)
    {
        var index = 0;
        return (char[] buffer, out int read, out int error) =>
        {
            var (text, err) = chunks[index++]();
            text.CopyTo(0, buffer, 0, text.Length);
            read = text.Length;
            error = err;
            return true;
        };
    }

    // 995 without a cancelled session re-reads instead of returning an empty line.
    [Fact]
    public async Task WindowsRead_995WithoutCancel_ContinuesReading()
    {
        var time = new SignalingFakeTimeProvider();
        var read = Chunks(() => ("", 995), () => ("y\r\n", 0));
        var line = Task.Run(() => ConsoleLineInput.ReadWindowsConsoleLine(read, CancellationToken.None, time));

        await time.WaitForTimerAsync(ConsoleLineInput.InterruptWait, TimeSpan.FromSeconds(60));
        time.Advance(ConsoleLineInput.InterruptWait);

        Assert.Equal("y", await line.WaitAsync(TimeSpan.FromSeconds(60)));
    }

    // The approval prompt reads through the injected console-aware reader.
    [Fact]
    public async Task ApprovalPrompt_UsesConsoleAwareReader_OnWindows()
    {
        var time = new SignalingFakeTimeProvider();
        var read = Chunks(() => ("", 995), () => ("y\r\n", 0));
        var approval = new ConsoleApprovalPrompt(null, null,
            ct => Task.Run(() => ConsoleLineInput.ReadWindowsConsoleLine(read, ct, time)));
        var reader = new LockingBlockingReader();
        using var console = new ConsoleSwap(reader);
        try
        {
            var tool = approval.RequestRewriteAsync("powershell", "az group delete", CancellationToken.None);
            var pump = Task.Run(() => approval.PumpOneAsync(CancellationToken.None));

            await time.WaitForTimerAsync(ConsoleLineInput.InterruptWait, TimeSpan.FromSeconds(60));
            time.Advance(ConsoleLineInput.InterruptWait);

            Assert.True((await pump.WaitAsync(TimeSpan.FromSeconds(60))).Approved);
            Assert.True(await tool.WaitAsync(TimeSpan.FromSeconds(60)));
        }
        finally
        {
            reader.Release.Set();
        }
    }

    // 995 after partial input is still an interrupt.
    [Fact]
    public async Task WindowsRead_995AfterPartialInput_IsInterrupted()
    {
        using var session = new CancellationTokenSource();
        var read = Chunks(() => ("ab", 0), () => { session.Cancel(); return ("", 995); });

        var line = Task.Run(() => ConsoleLineInput.ReadWindowsConsoleLine(read, session.Token, new FakeTimeProvider()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => line.WaitAsync(TimeSpan.FromSeconds(60)));
    }
}
