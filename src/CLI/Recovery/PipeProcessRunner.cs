// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace Core.Cli.Recovery;

internal static class PipeProcessRunner
{
    internal static async Task<int> RunAsync<T>(
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
            await StopAsync(process).ConfigureAwait(false);
            await input.DisposeAsync().ConfigureAwait(false);
            await ObserveClosedPipeAsync(sent).ConfigureAwait(false);
            await ObserveClosedPipeAsync(received).ConfigureAwait(false);
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

    private static async Task StopAsync(Process process)
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
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
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
