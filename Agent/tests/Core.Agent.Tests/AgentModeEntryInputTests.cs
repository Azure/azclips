using System.Diagnostics;
using Core.Agent.AgentMode;
using Core.Agent.Hosting;
using Core.Agent.Telemetry;
using Tests.Common.Utilities;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>Chat turns driven by the test; each send runs the next scripted step.</summary>
internal sealed class ScriptedTurns : AgentModeEntry.IChatTurns
{
    private readonly Queue<Func<CancellationToken, Task>> _steps = new();
    private TaskCompletionSource _aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly List<string> Prompts = [];
    public int Aborts;

    public bool SessionClosed => false;
    public SessionErrored? LastError { get; set; }
    public bool LastSendCalledClosingTool => true;
    public Activity? StartActivity(string name) => null;

    public ScriptedTurns Then(Func<CancellationToken, Task> step)
    {
        _steps.Enqueue(step);
        return this;
    }

    public Task Aborted => _aborted.Task;

    public Task SendAsync(string prompt, CancellationToken ct)
    {
        Prompts.Add(prompt);
        LastError = null;
        _aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _steps.Count > 0 ? _steps.Dequeue()(ct) : Task.CompletedTask;
    }

    public Task AbortAsync(CancellationToken ct)
    {
        Aborts++;
        _aborted.TrySetResult();
        return Task.CompletedTask;
    }

    /// <summary>A turn that runs until the host aborts it.</summary>
    public async Task UntilAborted()
    {
        await Aborted;
        throw new OperationCanceledException();
    }
}

/// <summary>Console writer that signals when the approval prompt has been written.</summary>
internal sealed class PromptSignalWriter : StringWriter
{
    public readonly TaskCompletionSource ApprovalShown = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Write(string? value)
    {
        base.Write(value);
        if (value?.Contains("[y]es", StringComparison.Ordinal) == true)
            ApprovalShown.TrySetResult();
    }
}

[Collection("Console")]
public class AgentModeEntryInputTests
{
    private static async Task<(AgentModeEntry.ChatLoopEnd end, string output)> RunLoopAsync(
        ScriptedTurns turns, FakeKeySource? source, ConsoleApprovalPrompt? approval = null,
        bool interactive = true, Func<PromptSignalWriter, Task>? whileRunning = null)
    {
        using var keys = source is null ? null : new ConsoleKeyReader(source);
        keys?.Start();
        approval ??= new ConsoleApprovalPrompt(keys: keys);
        var writer = new PromptSignalWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var loop = AgentModeEntry.RunChatLoopAsync(turns, approval, keys, spinner: null,
                "first", interactive, CancellationToken.None);
            if (whileRunning is not null)
                await whileRunning(writer).WaitAsync(AgentTestTimeouts.HangGuard);
            var end = await loop.WaitAsync(AgentTestTimeouts.HangGuard);
            return (end, writer.ToString());
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    private static ConsoleApprovalPrompt NewApproval(FakeKeySource source, out ConsoleKeyReader keys)
    {
        keys = new ConsoleKeyReader(source);
        return new ConsoleApprovalPrompt(keys: keys);
    }

    [Fact]
    public async Task IdleCtrlC_WithEmptyDraft_QuitsTheLoop()
    {
        var source = new FakeKeySource();
        source.Type(Keys.CtrlC);
        var (end, _) = await RunLoopAsync(new ScriptedTurns(), source);
        Assert.Equal(AgentModeEntry.ChatLoopEnd.CtrlCQuit, end);
    }

    [Fact]
    public async Task CtrlC_WithDraft_ClearsTheDraftOnly()
    {
        var source = new FakeKeySource();
        source.Type("draft");
        source.Type(Keys.CtrlC);
        source.Type("next");
        source.Type(Keys.Enter);
        source.Type("exit");
        source.Type(Keys.Enter);
        var turns = new ScriptedTurns();
        var (end, _) = await RunLoopAsync(turns, source);
        Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        Assert.Equal(["first", "next"], turns.Prompts);
    }

    [Fact]
    public async Task IdleCtrlCQuit_ExitsWith130()
    {
        await AgentLayerTestHarness.RunAsync(new AgentLayerTestHarness(), async harness =>
        {
            var scope = harness.CreateScope();
            var source = new FakeKeySource();
            source.Type(Keys.CtrlC);
            var code = await AgentModeEntry.RunSessionAsync(() => scope, async (_, _) =>
            {
                var (end, _) = await RunLoopAsync(new ScriptedTurns(), source);
                AgentModeEntry.ThrowIfQuitByCtrlC(end);
            }, () => { }, harness.Clock);
            Assert.Equal(130, code);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EscOrCtrlC_WhileThinking_InterruptsOnlyTheTurn_AndReturnsToPrompt(bool esc)
    {
        var source = new FakeKeySource();
        var turns = new ScriptedTurns();
        turns.Then(_ => turns.UntilAborted());
        source.Type(esc ? Keys.Esc : Keys.CtrlC);
        source.Type("again");
        source.Type(Keys.Enter);
        source.Type("exit");
        source.Type(Keys.Enter);

        var (end, output) = await RunLoopAsync(turns, source);

        Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        Assert.Equal(1, turns.Aborts);
        Assert.Equal(["first", "again"], turns.Prompts);
        Assert.Contains("aborted by user", output);
    }

    [Fact]
    public async Task SdkError_IsShown_AndTheSessionReturnsToPrompt()
    {
        var source = new FakeKeySource();
        var turns = new ScriptedTurns();
        turns.Then(_ =>
        {
            turns.LastError = new SessionErrored("rate_limit", "offline failure", null, 429, null, DateTimeOffset.UtcNow);
            return Task.FromException(new OperationCanceledException());
        });
        source.Type("retry");
        source.Type(Keys.Enter);
        source.Type("exit");
        source.Type(Keys.Enter);

        var (end, output) = await RunLoopAsync(turns, source);

        Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        Assert.Equal(["first", "retry"], turns.Prompts);
        Assert.Contains("offline failure", output);
    }

    [Fact]
    public async Task CtrlC_AtApproval_DeniesAndInterruptsTheTurn()
    {
        var source = new FakeKeySource();
        var approval = NewApproval(source, out var keys);
        using var _ = keys;
        keys.Start();
        var turns = new ScriptedTurns();
        Task<bool>? tool = null;
        turns.Then(_ =>
        {
            tool = approval.RequestRewriteAsync("powershell", "az group delete", CancellationToken.None);
            return turns.UntilAborted();
        });

        var prevOut = Console.Out;
        var writer = new PromptSignalWriter();
        Console.SetOut(writer);
        try
        {
            var loop = AgentModeEntry.RunChatLoopAsync(turns, approval, keys, null, "first", true, CancellationToken.None);
            await writer.ApprovalShown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            source.Type(Keys.CtrlC);
            await turns.Aborted.WaitAsync(AgentTestTimeouts.HangGuard);
            source.Type("exit");
            source.Type(Keys.Enter);
            Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, await loop.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            Console.SetOut(prevOut);
        }

        Assert.False(await tool!.WaitAsync(AgentTestTimeouts.HangGuard));
        Assert.Equal(1, turns.Aborts);
    }

    [Fact]
    public async Task EscTypedBeforeApprovalOpens_IsNotLost()
    {
        var source = new FakeKeySource();
        var approval = NewApproval(source, out var keys);
        using var _ = keys;
        var turns = new ScriptedTurns();
        Task<bool>? tool = null;
        source.Type(Keys.Esc);
        source.Type("exit");
        source.Type(Keys.Enter);
        keys.Start();
        await keys.WaitToReadAsync(CancellationToken.None).AsTask().WaitAsync(AgentTestTimeouts.HangGuard);
        turns.Then(_ =>
        {
            tool = approval.RequestRewriteAsync("powershell", "az group delete", CancellationToken.None);
            return turns.UntilAborted();
        });

        var prevOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            var end = await AgentModeEntry.RunChatLoopAsync(turns, approval, keys, null, "first", true, CancellationToken.None)
                .WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        }
        finally
        {
            Console.SetOut(prevOut);
        }

        Assert.Equal(1, turns.Aborts);
        Assert.False(await tool!.WaitAsync(AgentTestTimeouts.HangGuard));
    }

    [Fact]
    public async Task ApprovalCancelledByAFinishedTurn_DoesNotOpen()
    {
        var turns = new ScriptedTurns();
        var approval = new ConsoleApprovalPrompt();
        using var turnCts = new CancellationTokenSource();
        Task<bool>? tool = null;
        turns.Then(_ =>
        {
            tool = approval.RequestRewriteAsync("powershell", "az group delete", turnCts.Token);
            turnCts.Cancel();
            turns.LastError = new SessionErrored("unknown", "offline failure", null, null, null, DateTimeOffset.UtcNow);
            return Task.FromException(new OperationCanceledException());
        });

        var (end, output) = await RunLoopAsync(turns, source: null, approval, interactive: false);

        Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        Assert.DoesNotContain("[y]es", output);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool!);
    }

    [Fact]
    public async Task ContinuousTyping_DoesNotKeepTheApprovalFromOpening()
    {
        var source = new FakeKeySource();
        var approval = NewApproval(source, out var keys);
        using var _ = keys;
        keys.Start();
        var turns = new ScriptedTurns();
        Task<bool>? tool = null;
        turns.Then(async _ =>
        {
            tool = approval.RequestRewriteAsync("powershell", "az group delete", CancellationToken.None);
            await tool;
            await turns.UntilAborted();
        });

        var prevOut = Console.Out;
        var writer = new PromptSignalWriter();
        Console.SetOut(writer);
        try
        {
            source.Type("zzzzzzzz");
            var loop = AgentModeEntry.RunChatLoopAsync(turns, approval, keys, null, "first", true, CancellationToken.None);
            await writer.ApprovalShown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            // Esc at the approval is "n"; the turn keeps running until Esc in the thinking view.
            source.Type(Keys.Esc);
            Assert.False(await tool!.WaitAsync(AgentTestTimeouts.HangGuard));
            Assert.Equal(0, turns.Aborts);
            source.Type(Keys.Esc);
            await turns.Aborted.WaitAsync(AgentTestTimeouts.HangGuard);
            source.Type("exit");
            source.Type(Keys.Enter);
            await loop.WaitAsync(AgentTestTimeouts.HangGuard);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task Oneshot_ReadsNoKeys()
    {
        Assert.False(AgentModeEntry.ReadsKeys(interactive: false, inputRedirected: false, Console.In, Console.In));
        var turns = new ScriptedTurns();
        var (end, _) = await RunLoopAsync(turns, source: null, interactive: false);
        Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        Assert.Equal(0, turns.Aborts);
    }

    [Fact]
    public void RedirectedStdinOrSetIn_UsesLineReads()
    {
        var original = Console.In;
        Assert.False(AgentModeEntry.ReadsKeys(true, inputRedirected: true, original, original));
        Assert.False(AgentModeEntry.ReadsKeys(true, inputRedirected: false, new StringReader("y"), original));
        Assert.True(AgentModeEntry.ReadsKeys(true, inputRedirected: false, original, original));
    }

    [Fact]
    public async Task SetIn_LineInput_DrivesThePromptLoop()
    {
        var turns = new ScriptedTurns();
        var prevIn = Console.In;
        Console.SetIn(new StringReader("second\nexit\n"));
        try
        {
            var (end, _) = await RunLoopAsync(turns, source: null);
            Assert.Equal(AgentModeEntry.ChatLoopEnd.Finished, end);
        }
        finally
        {
            Console.SetIn(prevIn);
        }
        Assert.Equal(["first", "second"], turns.Prompts);
    }

    [Fact]
    public async Task UncooperativeCleanup_IsBoundedByTheSharedDeadline_ThenTheTerminalIsRestored()
    {
        var clock = new SignalingFakeTimeProvider();
        await AgentLayerTestHarness.RunAsync(new AgentLayerTestHarness(timeProvider: clock), async harness =>
        {
            var scope = harness.CreateScope();
            var source = new FakeKeySource();
            var keys = new ConsoleKeyReader(source);
            keys.Start();
            var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var running = AgentModeEntry.RunSessionAsync(() => scope, (_, lifetime) =>
            {
                lifetime.RegisterCleanup(async () =>
                {
                    cleanupEntered.TrySetResult();
                    await release.Task;
                });
                return Task.CompletedTask;
            }, () => { }, clock, restoreTerminal: keys.Dispose);

            await cleanupEntered.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            await clock.WaitForTimerAsync(due => due == AgentRuntimeLifetime.ProducerDrainBudget, AgentTestTimeouts.HangGuard);
            Assert.False(running.IsCompleted);
            Assert.Equal(0, source.RestoreCount);
            clock.Advance(AgentRuntimeLifetime.ProducerDrainBudget);

            Assert.Equal(0, await running.WaitAsync(AgentTestTimeouts.HangGuard));
            Assert.Equal(1, source.RestoreCount);
            release.SetResult();
        });
    }
}
