using System.Collections.Concurrent;
using Core.Agent.Hosting;
using Tests.Common.Utilities;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>Key source fed by the test; ReadKey blocks until the test adds a key.</summary>
internal sealed class FakeKeySource : IConsoleKeySource
{
    private readonly BlockingCollection<ConsoleKeyInfo> _keys = new();
    public int EnterCount;
    public int RestoreCount;

    public void EnterKeyMode() => Interlocked.Increment(ref EnterCount);
    public void RestoreMode() => Interlocked.Increment(ref RestoreCount);

    public ConsoleKeyInfo? ReadKey()
    {
        try { return _keys.Take(); }
        catch (InvalidOperationException) { return null; }
    }

    public void Type(params ConsoleKeyInfo[] keys)
    {
        foreach (var key in keys) _keys.Add(key);
    }

    public void Type(string text)
    {
        foreach (var ch in text) _keys.Add(Keys.Char(ch));
    }

    public void End() => _keys.CompleteAdding();
}

internal static class Keys
{
    public static ConsoleKeyInfo Char(char ch) => new(ch, ch switch
    {
        >= 'a' and <= 'z' => ConsoleKey.A + (ch - 'a'),
        >= 'A' and <= 'Z' => ConsoleKey.A + (ch - 'A'),
        ' ' => ConsoleKey.Spacebar,
        _ => default,
    }, char.IsUpper(ch), false, false);

    public static readonly ConsoleKeyInfo Enter = new('\r', ConsoleKey.Enter, false, false, false);
    public static readonly ConsoleKeyInfo Esc = new('\x1b', ConsoleKey.Escape, false, false, false);
    public static readonly ConsoleKeyInfo CtrlC = new('\x03', ConsoleKey.C, false, false, true);
    public static readonly ConsoleKeyInfo CtrlD = new('\x04', ConsoleKey.D, false, false, true);
    public static readonly ConsoleKeyInfo CtrlZ = new('\x1a', ConsoleKey.Z, false, false, true);
    public static readonly ConsoleKeyInfo Backspace = new('\b', ConsoleKey.Backspace, false, false, false);
    public static readonly ConsoleKeyInfo Left = new('\0', ConsoleKey.LeftArrow, false, false, false);
    public static readonly ConsoleKeyInfo Right = new('\0', ConsoleKey.RightArrow, false, false, false);
    public static readonly ConsoleKeyInfo Home = new('\0', ConsoleKey.Home, false, false, false);
    public static readonly ConsoleKeyInfo End = new('\0', ConsoleKey.End, false, false, false);
}

/// <summary>Writer that signals when a flush has written <c>needle</c>.</summary>
internal sealed class FlushSignalWriter(string needle) : StringWriter
{
    public readonly TaskCompletionSource Shown = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Flush()
    {
        base.Flush();
        if (ToString().Contains(needle, StringComparison.Ordinal))
            Shown.TrySetResult();
    }
}

public class ConsoleKeyReaderTests
{
    [Theory]
    [InlineData(true, true, true, CtrlCAction.CancelApproval)]
    [InlineData(true, false, false, CtrlCAction.CancelApproval)]
    [InlineData(false, true, true, CtrlCAction.ClearDraft)]
    [InlineData(false, true, false, CtrlCAction.ClearDraft)]
    [InlineData(false, false, true, CtrlCAction.InterruptTurn)]
    [InlineData(false, false, false, CtrlCAction.Quit)]
    public void CtrlC_FollowsApprovalDraftTurnQuitOrder(bool approval, bool draft, bool turn, CtrlCAction expected) =>
        Assert.Equal(expected, ConsoleKeyReader.DecideCtrlC(approval, draft, turn));

    [Fact]
    public void ThinkingView_EscAndCtrlCInterruptTheTurn_OtherKeysDoNot()
    {
        Assert.True(AbortKeyWatcher.InterruptsTurn(Keys.Esc));
        Assert.True(AbortKeyWatcher.InterruptsTurn(Keys.CtrlC));
        Assert.False(AbortKeyWatcher.InterruptsTurn(Keys.Char('y')));
        Assert.False(AbortKeyWatcher.InterruptsTurn(Keys.Enter));
    }

    [Fact]
    public void KeyInput_OnlyOnARealConsoleWithTheOriginalReader()
    {
        var original = new StringReader("");
        Assert.True(ConsoleKeyReader.UsesKeyInput(false, original, original));
        Assert.False(ConsoleKeyReader.UsesKeyInput(true, original, original));
        // Console.SetIn does not change IsInputRedirected; the replaced reader is detected.
        Assert.False(ConsoleKeyReader.UsesKeyInput(false, new StringReader("y"), original));
    }

    [Fact]
    public async Task ReadLine_SubmitsTypedText_AndEntersAndRestoresKeyMode()
    {
        var source = new FakeKeySource();
        var reader = new ConsoleKeyReader(source);
        reader.Start();
        source.Type("hello");
        source.Type(Keys.Enter);

        var read = await reader.ReadLineAsync(new LineEditor(), "> ", TextWriter.Null, CancellationToken.None)
            .WaitAsync(AgentTestTimeouts.HangGuard);

        Assert.Equal(new LineRead(LineReadKind.Submitted, "hello"), read);
        Assert.Equal(1, source.EnterCount);
        reader.Dispose();
        Assert.Equal(1, source.RestoreCount);
    }

    [Fact]
    public async Task Paste_WithCtrlCInside_StopsAtCtrlC_AndKeepsTheRestQueued()
    {
        var source = new FakeKeySource();
        using var reader = new ConsoleKeyReader(source);
        source.Type("ab");
        source.Type(Keys.CtrlC);
        source.Type("cd");
        source.Type(Keys.Enter);
        reader.Start();

        var editor = new LineEditor();
        var first = await reader.ReadLineAsync(editor, "> ", TextWriter.Null, CancellationToken.None)
            .WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.Equal(LineReadKind.CtrlC, first.Kind);
        Assert.Equal("ab", editor.Text);

        editor.Clear();
        var second = await reader.ReadLineAsync(editor, "> ", TextWriter.Null, CancellationToken.None)
            .WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.Equal(new LineRead(LineReadKind.Submitted, "cd"), second);
    }

    [Fact]
    public async Task Esc_EndsTheRead()
    {
        var source = new FakeKeySource();
        using var reader = new ConsoleKeyReader(source);
        source.Type("x");
        source.Type(Keys.Esc);
        reader.Start();

        var read = await reader.ReadLineAsync(new LineEditor(), "> ", TextWriter.Null, CancellationToken.None)
            .WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.Equal(LineReadKind.Escape, read.Kind);
    }

    [Fact]
    public async Task EndOfKeys_IsEndOfInput()
    {
        var source = new FakeKeySource();
        using var reader = new ConsoleKeyReader(source);
        reader.Start();
        source.End();

        var read = await reader.ReadLineAsync(new LineEditor(), "> ", TextWriter.Null, CancellationToken.None)
            .WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.Equal(LineReadKind.EndOfInput, read.Kind);
    }

    [Fact]
    public async Task CancelledRead_LeavesLaterKeysForTheNextView()
    {
        var source = new FakeKeySource();
        using var reader = new ConsoleKeyReader(source);
        reader.Start();
        using var stop = new CancellationTokenSource();
        var pending = reader.ReadLineAsync(new LineEditor(), "> ", TextWriter.Null, stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(AgentTestTimeouts.HangGuard));

        source.Type(Keys.Esc);
        Assert.True(await reader.WaitToReadAsync(CancellationToken.None).AsTask().WaitAsync(AgentTestTimeouts.HangGuard));
        Assert.True(AbortKeyWatcher.TakeTurnKeys(reader));
    }

    [Fact]
    public void IsCtrlC_RawControlCharacter_WithoutModifier()
    {
        Assert.True(ConsoleKeyReader.IsCtrlC(new ConsoleKeyInfo('\x03', default, false, false, false)));
    }

    [Fact]
    public void IsCtrlC_ControlModifierAndC_WithoutRawCharacter()
    {
        Assert.True(ConsoleKeyReader.IsCtrlC(new ConsoleKeyInfo('\0', ConsoleKey.C, false, false, true)));
        Assert.False(ConsoleKeyReader.IsCtrlC(new ConsoleKeyInfo('c', ConsoleKey.C, false, false, false)));
    }

    [Fact]
    public void SystemConsoleKeySource_RestoresTheSavedControlCMode()
    {
        var mode = false;
        var source = new SystemConsoleKeySource(() => mode, value => mode = value);
        source.EnterKeyMode();
        Assert.True(mode);
        source.RestoreMode();
        Assert.False(mode);
    }

    [Fact]
    public async Task EachKeyBatch_IsRedrawnBeforeEnter()
    {
        var source = new FakeKeySource();
        using var reader = new ConsoleKeyReader(source);
        reader.Start();
        var output = new FlushSignalWriter("> ab");
        var read = reader.ReadLineAsync(new LineEditor(() => 80), "> ", output, CancellationToken.None);
        source.Type("ab");
        // The redraw shows "ab" before Enter arrives.
        await output.Shown.Task.WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.False(read.IsCompleted);
        source.Type(Keys.Enter);
        Assert.Equal(new LineRead(LineReadKind.Submitted, "ab"), await read.WaitAsync(AgentTestTimeouts.HangGuard));
    }

    [Fact]
    public async Task ThinkingView_DiscardsTypedKeys_AndKeepsKeysAfterTheInterrupt()
    {
        var source = new FakeKeySource();
        using var reader = new ConsoleKeyReader(source);
        source.Type("zz");
        source.Type(Keys.Esc);
        source.Type("y");
        source.Type(Keys.Enter);
        reader.Start();

        var read = Task.Run(async () =>
        {
            while (!AbortKeyWatcher.TakeTurnKeys(reader))
                await reader.WaitToReadAsync(CancellationToken.None);
        });
        await read.WaitAsync(AgentTestTimeouts.HangGuard);

        var next = await reader.ReadLineAsync(new LineEditor(), "> ", TextWriter.Null, CancellationToken.None)
            .WaitAsync(AgentTestTimeouts.HangGuard);
        Assert.Equal(new LineRead(LineReadKind.Submitted, "y"), next);
    }
}
