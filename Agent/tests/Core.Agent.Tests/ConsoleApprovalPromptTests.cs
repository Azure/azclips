using Core.Agent.Hosting;
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
/// Contract: y/yes → approve; n/no/empty, Esc and EOF → reject; Ctrl+C → reject and
/// interrupt the turn; anything else → reject with the typed text returned as
/// FeedbackPrompt for the next chat turn.
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

    [Fact]
    public async Task Eof_OnLineInput_Denies()
    {
        var prompt = new ConsoleApprovalPrompt();
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(new StringReader(""));
        Console.SetOut(TextWriter.Null);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var outcome = await prompt.PumpOneAsync(CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.No, outcome);
            Assert.False(await toolTask);
        }
        finally
        {
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }

    private static async Task<(ConsoleApprovalPrompt.ApprovalOutcome outcome, bool toolApproved)>
        DriveKeysAsync(Action<FakeKeySource> type)
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        type(source);
        keys.Start();
        var prompt = new ConsoleApprovalPrompt(keys: keys);
        var prevOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var outcome = await prompt.PumpOneAsync(CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard);
            return (outcome, await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("yes", true)]
    [InlineData("n", false)]
    [InlineData("no", false)]
    public async Task KeyInput_YApproves_NDenies(string typed, bool expected)
    {
        var (outcome, approved) = await DriveKeysAsync(s => { s.Type(typed); s.Type(Keys.Enter); });
        Assert.Equal(expected, outcome.Approved);
        Assert.Equal(expected, approved);
        Assert.Null(outcome.FeedbackPrompt);
    }

    [Fact]
    public async Task KeyInput_FreeText_IsRedirect()
    {
        var (outcome, approved) = await DriveKeysAsync(s => { s.Type("use rg2"); s.Type(Keys.Enter); });
        Assert.False(approved);
        Assert.Equal("use rg2", outcome.FeedbackPrompt);
    }

    [Fact]
    public async Task KeyInput_BackspaceEdit_IsAppliedBeforeTheAnswer()
    {
        var (outcome, approved) = await DriveKeysAsync(s => { s.Type("n"); s.Type(Keys.Backspace); s.Type("y"); s.Type(Keys.Enter); });
        Assert.True(approved);
        Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.Yes, outcome);
    }

    [Fact]
    public async Task KeyInput_Esc_IsNo()
    {
        var (outcome, approved) = await DriveKeysAsync(s => { s.Type("redirect text"); s.Type(Keys.Esc); });
        Assert.False(approved);
        Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.No, outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyInput_Eof_Denies(bool ctrlD)
    {
        var (outcome, approved) = await DriveKeysAsync(s => { if (ctrlD) s.Type(Keys.CtrlD); else s.End(); });
        Assert.False(approved);
        Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.No, outcome);
    }

    [Fact]
    public async Task KeyInput_CtrlC_DeniesAndInterruptsTheTurn()
    {
        var (outcome, approved) = await DriveKeysAsync(s => { s.Type("y"); s.Type(Keys.CtrlC); });
        Assert.False(approved);
        Assert.True(outcome.InterruptTurn);
    }

    [Fact]
    public async Task CancelledRequest_IsNotPrompted()
    {
        var prompt = new ConsoleApprovalPrompt();
        using var turn = new CancellationTokenSource();
        var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", turn.Token);
        turn.Cancel();
        var outcome = await prompt.PumpOneAsync(CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.NotHandled, outcome);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toolTask);
    }

    /// <summary>Renderer that runs a test callback inside RunExclusive before the body.</summary>
    private sealed class CallbackRenderer(Action beforeBody) : IAgentRenderer
    {
        public void OnText(string chunk) { }
        public void OnEvent(AgentEvent ev) { }
        public void OnSpinnerFrame(string frame, string verb) { }
        public void OnSpinnerStop() { }
        public void RunExclusive(Action action)
        {
            beforeBody();
            action();
        }
    }

    [Fact]
    public async Task RequestCancelledWhileWaitingToRender_NeverOpensARead()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        keys.Start();
        using var turn = new CancellationTokenSource();
        var prompt = new ConsoleApprovalPrompt(renderer: new CallbackRenderer(turn.Cancel), keys: keys);
        var writer = new StringWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", turn.Token);
            var outcome = await prompt.PumpOneAsync(CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.NotHandled, outcome);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toolTask);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
        Assert.DoesNotContain("[y]es", writer.ToString());
    }

    [Fact]
    public async Task PumpCancelledWhileRendering_NeverReadsALine()
    {
        using var pump = new CancellationTokenSource();
        var prompt = new ConsoleApprovalPrompt(renderer: new CallbackRenderer(pump.Cancel));
        var input = new StringReader("y" + Environment.NewLine);
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(input);
        Console.SetOut(TextWriter.Null);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var outcome = await prompt.PumpOneAsync(pump.Token).WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.NotHandled, outcome);
            Assert.False(await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
        Assert.Equal("y", input.ReadLine());
    }

    [Fact]
    public async Task AnsweredApproval_SettlesWithoutUsingADisposedTokenSource()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        source.Type("y");
        source.Type(Keys.Enter);
        keys.Start();
        var prompt = new ConsoleApprovalPrompt(keys: keys);
        var prevOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var outcome = await prompt.PumpOneAsync(CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.Yes, outcome);
            Assert.True(await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task RequestEndingWhileTheReadIsOpen_StopsTheRead()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        keys.Start();
        using var turn = new CancellationTokenSource();
        var prompt = new ConsoleApprovalPrompt(keys: keys);
        var writer = new PromptSignalWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", turn.Token);
            var pump = prompt.PumpOneAsync(CancellationToken.None);
            await writer.ApprovalShown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            turn.Cancel();
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.NotHandled, await pump.WaitAsync(AgentTestTimeouts.HangGuard));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toolTask);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task RendererOutput_WhileTheApprovalIsOpen_RedrawsTheApprovalLine()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        keys.Start();
        var renderer = new ConsoleAgentRenderer();
        var prompt = new ConsoleApprovalPrompt(renderer: renderer, keys: keys, output: renderer);
        var writer = new PromptSignalWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var pump = prompt.PumpOneAsync(CancellationToken.None);
            await writer.ApprovalShown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            var before = writer.ToString().Length;
            renderer.OnText("streamed text");
            var after = writer.ToString()[before..];
            var text = after.IndexOf("streamed text", StringComparison.Ordinal);
            Assert.True(text >= 0);
            Assert.Contains("[y]es", after[text..]);
            source.Type("n");
            source.Type(Keys.Enter);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.No, await pump.WaitAsync(AgentTestTimeouts.HangGuard));
            Assert.False(await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));

            var closed = writer.ToString().Length;
            renderer.OnText("later");
            Assert.DoesNotContain("[y]es", writer.ToString()[closed..]);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task EscAtApproval_EndsTheApprovalRow()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        source.Type("draft");
        source.Type(Keys.Esc);
        keys.Start();
        var prompt = new ConsoleApprovalPrompt(keys: keys);
        var writer = new StringWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.No,
                await prompt.PumpOneAsync(CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard));
            Assert.False(await toolTask);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
        Assert.EndsWith(Environment.NewLine, writer.ToString());
    }

    [Fact]
    public async Task KeyInput_AltGr_IsText()
    {
        var (outcome, approved) = await DriveKeysAsync(s =>
        {
            s.Type(new ConsoleKeyInfo('\u00A9', ConsoleKey.C, false, true, true));
            s.Type(Keys.Enter);
        });
        Assert.False(approved);
        Assert.Equal("\u00A9", outcome.FeedbackPrompt);
    }

    /// <summary>Line reader that ends the turn while the read is blocked, then returns the answer.</summary>
    private sealed class TurnEndingReader(CancellationTokenSource turn, string answer) : TextReader
    {
        public override string? ReadLine()
        {
            turn.Cancel();
            return answer;
        }
    }

    [Fact]
    public async Task TurnEndsDuringSynchronousRead_CannotApprove()
    {
        using var turn = new CancellationTokenSource();
        var prompt = new ConsoleApprovalPrompt();
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(new TurnEndingReader(turn, "y"));
        Console.SetOut(TextWriter.Null);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var outcome = await prompt.PumpOneAsync(turn.Token).WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.NotHandled, outcome);
            Assert.False(await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public async Task EndingOpenApproval_EndsItsLine()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        keys.Start();
        using var turn = new CancellationTokenSource();
        var prompt = new ConsoleApprovalPrompt(keys: keys);
        var writer = new PromptSignalWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var pump = prompt.PumpOneAsync(turn.Token);
            await writer.ApprovalShown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            turn.Cancel();
            Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.NotHandled, await pump.WaitAsync(AgentTestTimeouts.HangGuard));
            Assert.False(await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            Console.SetOut(prevOut);
        }
        Assert.EndsWith(Environment.NewLine, writer.ToString());
    }

    /// <summary>Renderer that signals the first RunExclusive call after it is armed.</summary>
    private sealed class ExclusiveSignalRenderer(ConsoleAgentRenderer inner) : IAgentRenderer
    {
        public volatile bool Armed;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnText(string chunk) => inner.OnText(chunk);
        public void OnEvent(AgentEvent ev) => inner.OnEvent(ev);
        public void OnSpinnerFrame(string frame, string verb) => inner.OnSpinnerFrame(frame, verb);
        public void OnSpinnerStop() => inner.OnSpinnerStop();
        public void RunExclusive(Action action)
        {
            if (Armed) Entered.TrySetResult();
            inner.RunExclusive(action);
        }
    }

    /// <summary>Writer whose first prompt write after it is armed blocks until released.</summary>
    private sealed class BlockingPromptWriter : StringWriter
    {
        public readonly TaskCompletionSource ApprovalShown = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ManualResetEventSlim Release = new();
        public volatile bool Armed;

        public override void Write(string? value)
        {
            if (value?.Contains("[y]es", StringComparison.Ordinal) == true)
            {
                ApprovalShown.TrySetResult();
                if (Armed)
                {
                    Armed = false;
                    Blocked.TrySetResult();
                    Release.Wait();
                }
            }
            lock (this) base.Write(value);
        }

        public override void Write(char value)
        {
            lock (this) base.Write(value);
        }
    }

    [Fact]
    public async Task ApprovalKeyEditing_IsSerializedWithRendererRedraw()
    {
        var source = new FakeKeySource();
        using var keys = new ConsoleKeyReader(source);
        keys.Start();
        var inner = new ConsoleAgentRenderer();
        var renderer = new ExclusiveSignalRenderer(inner);
        var prompt = new ConsoleApprovalPrompt(renderer: renderer, keys: keys, output: inner);
        var writer = new BlockingPromptWriter();
        var prevOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var toolTask = prompt.RequestRewriteAsync("powershell", "az group delete --yes", CancellationToken.None);
            var pump = prompt.PumpOneAsync(CancellationToken.None);
            await writer.ApprovalShown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            // The renderer redraws the approval line and stops inside that redraw.
            writer.Armed = true;
            var redraw = Task.Run(() => inner.OnText("streamed"));
            await writer.Blocked.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            // Keys arrive while the redraw holds the renderer lock.
            renderer.Armed = true;
            source.Type("abcdef");
            source.Type(Keys.Enter);
            await renderer.Entered.Task.WaitAsync(AgentTestTimeouts.HangGuard);
            writer.Release.Set();
            await redraw.WaitAsync(AgentTestTimeouts.HangGuard);
            Assert.Equal("abcdef", (await pump.WaitAsync(AgentTestTimeouts.HangGuard)).FeedbackPrompt);
            Assert.False(await toolTask.WaitAsync(AgentTestTimeouts.HangGuard));
        }
        finally
        {
            writer.Release.Set();
            Console.SetOut(prevOut);
        }
    }

    [Fact]
    public void Settle_EofDenies_FreeTextRedirects()
    {
        var eof = new TaskCompletionSource<bool>();
        Assert.Equal(ConsoleApprovalPrompt.ApprovalOutcome.No,
            ConsoleApprovalPrompt.Settle(eof, new LineRead(LineReadKind.EndOfInput)));
        Assert.False(eof.Task.Result);
        var text = new TaskCompletionSource<bool>();
        Assert.Equal("use rg2", ConsoleApprovalPrompt.Settle(text, new LineRead(LineReadKind.Submitted, "use rg2")).FeedbackPrompt);
        Assert.False(text.Task.Result);
    }
}
