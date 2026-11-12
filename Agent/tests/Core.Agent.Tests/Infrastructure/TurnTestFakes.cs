// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Core.Agent.AgentMode;
using Core.Agent.Hosting;

namespace Core.Agent.Tests;

/// <summary>Key source for <see cref="AbortKeyWatcher"/> that the test fills and observes.</summary>
/// <remarks>
/// <see cref="StdinLock"/> models the .NET Unix <c>SyncTextReader</c>: <see cref="KeyAvailable"/> and
/// <see cref="ReadKey"/> take the same lock that a blocked <see cref="LockingBlockingReader"/> holds.
/// </remarks>
internal sealed class ScriptedConsoleKeys : IConsoleKeys
{
    private readonly ConcurrentQueue<ConsoleKeyInfo> _keys = new();
    private int _keyAvailableCalls;
    private int _readKeyCalls;

    internal object StdinLock { get; } = new();
    internal int KeyAvailableCalls => Volatile.Read(ref _keyAvailableCalls);
    internal int ReadKeyCalls => Volatile.Read(ref _readKeyCalls);
    internal int Pending => _keys.Count;

    /// <summary>Runs after each <see cref="KeyAvailable"/> with its result, before it returns.</summary>
    internal Action<bool>? AfterKeyAvailable { get; set; }

    internal static ConsoleKeyInfo Esc => new('\x1b', ConsoleKey.Escape, false, false, false);
    internal static ConsoleKeyInfo CtrlC => new('\x03', ConsoleKey.C, false, false, true);
    internal static ConsoleKeyInfo Char(char c) => new(c, ConsoleKey.A, false, false, false);

    internal void Push(ConsoleKeyInfo key) => _keys.Enqueue(key);

    public bool KeyAvailable
    {
        get
        {
            Interlocked.Increment(ref _keyAvailableCalls);
            bool available;
            lock (StdinLock) available = !_keys.IsEmpty;
            AfterKeyAvailable?.Invoke(available);
            return available;
        }
    }

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        Interlocked.Increment(ref _readKeyCalls);
        lock (StdinLock)
            return _keys.TryDequeue(out var key) ? key : throw new InvalidOperationException("No key is buffered.");
    }
}

/// <summary>Console.In replacement whose ReadLine blocks until released, holding <c>stdinLock</c> like SyncTextReader.</summary>
internal sealed class LockingBlockingReader(object? stdinLock = null) : TextReader
{
    private readonly object _lock = stdinLock ?? new object();
    internal ManualResetEventSlim Entered { get; } = new();
    internal ManualResetEventSlim Release { get; } = new();
    internal string? Line { get; set; }

    public override string? ReadLine()
    {
        lock (_lock)
        {
            Entered.Set();
            Release.Wait();
            return Line;
        }
    }
}

/// <summary>A turn whose send the test completes; <see cref="AbortAsync"/> cancels the send, optionally after a hold.</summary>
internal sealed class FakeTurnAgent : ITurnAgent
{
    internal TaskCompletionSource Send { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource AbortEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource AbortRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool HoldAbort { get; init; }
    internal int AbortCalls;

    public SessionErrored? LastError => null;

    public Task SendAsync(string prompt, CancellationToken ct)
    {
        ct.Register(() => Send.TrySetCanceled(ct));
        return Send.Task;
    }

    public async Task AbortAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref AbortCalls);
        AbortEntered.TrySetResult();
        if (HoldAbort) await AbortRelease.Task;
        Send.TrySetCanceled();
    }
}

/// <summary>Swaps Console.In and silences Console.Out for one test.</summary>
internal sealed class ConsoleSwap : IDisposable
{
    private readonly TextReader _in = Console.In;
    private readonly TextWriter _out = Console.Out;

    internal ConsoleSwap(TextReader input)
    {
        Console.SetIn(input);
        Console.SetOut(TextWriter.Null);
    }

    public void Dispose()
    {
        Console.SetIn(_in);
        Console.SetOut(_out);
    }
}

internal static class TurnPump
{
    internal static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(50);

    /// <summary>Runs one turn of <see cref="AgentModeEntry.SendAndPumpAsync"/> off the test thread.</summary>
    internal static Task<(string? Redirect, bool UserAborted)> RunAsync(
        ITurnAgent agent, ConsoleApprovalPrompt approval, AbortKeyWatcher watcher, bool interactive,
        Func<bool> sessionCancelled, CancellationToken ct) =>
        Task.Run(() => AgentModeEntry.SendAndPumpAsync(
            agent, approval, watcher, spinner: null, "prompt", interactive, sessionCancelled, ct));

    internal static AbortKeyWatcher Watcher(ITurnAgent agent, IConsoleKeys keys, TimeProvider time, Action? onQuit = null) =>
        new(agent.AbortAsync, onQuit, Poll, keys, time);
}
