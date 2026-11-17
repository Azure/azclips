// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace Core.Runtime.Recovery;

public static class PipeProcessRunner
{
    private static readonly TimeSpan ExitWaitAfterKill = TimeSpan.FromSeconds(5);

    public static async Task<int> RunAsync<T>(
        Func<string, ProcessStartInfo> createStartInfo,
        T request,
        CancellationToken cancellationToken,
        Func<Process, CancellationToken, Task>? receive = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var ioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        using var process = Process.Start(createStartInfo(input.GetClientHandleAsString()))
            ?? throw new InvalidOperationException("Could not start the recovery process.");
        input.DisposeLocalCopyOfClientHandle();

        Task sent = Task.CompletedTask;
        Task received = Task.CompletedTask;
        try
        {
            sent = SendAsync(input, request, ioCancellation.Token);
            received = receive?.Invoke(process, ioCancellation.Token) ?? Task.CompletedTask;
            var exited = process.WaitForExitAsync(ioCancellation.Token);
            await CompleteInputAsync(input, process, sent, exited).ConfigureAwait(false);
            await received.ConfigureAwait(false);
            await exited.ConfigureAwait(false);
            return process.ExitCode;
        }
        catch
        {
            ioCancellation.Cancel();
            var exited = await StopAsync(process).ConfigureAwait(false);
            await input.DisposeAsync().ConfigureAwait(false);
            await ObserveClosedPipeAsync(sent).ConfigureAwait(false);
            if (exited)
                await ObserveClosedPipeAsync(received).ConfigureAwait(false);
            else
                _ = ObserveClosedPipeAsync(received);
            throw;
        }
    }

    private static async Task CompleteInputAsync(
        Stream input, Process process, Task sent, Task exited)
    {
        if (await Task.WhenAny(sent, exited).ConfigureAwait(false) == exited && !sent.IsCompleted)
        {
            await input.DisposeAsync().ConfigureAwait(false);
            await ObserveClosedPipeAsync(sent).ConfigureAwait(false);
            await exited.ConfigureAwait(false);
            throw new InvalidOperationException(
                $"The recovery process exited before accepting its input (exit code {process.ExitCode}).");
        }
        await sent.ConfigureAwait(false);
    }

    private static async Task SendAsync<T>(Stream input, T request, CancellationToken cancellationToken)
    {
        try
        {
            await JsonSerializer.SerializeAsync(input, request, cancellationToken: cancellationToken).ConfigureAwait(false);
            await input.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await input.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Kills the child and waits a bounded time for it to exit; returns whether it exited.</summary>
    private static async Task<bool> StopAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception && process.HasExited)
        {
            // The child completed between checking its state and terminating it.
        }
        try
        {
            await process.WaitForExitAsync().WaitAsync(ExitWaitAfterKill).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            // The caller rethrows the original failure without waiting on the child's open pipes.
            return false;
        }
    }

    private static async Task ObserveClosedPipeAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or JsonException)
        {
            // Terminating the child closes its outstanding I/O; the original failure takes precedence.
        }
    }
}
