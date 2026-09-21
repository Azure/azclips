// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Core.Abstractions.Recovery;

namespace Core.Cli.Recovery;

internal static class AgentProcessRunner
{
    internal static ProcessStartInfo BuildManualStartInfo(string agentExe, string inputHandle, string? traceparent)
    {
        var start = Program.BuildAgentStartInfo(agentExe, [], null, traceparent);
        start.ArgumentList.Clear();
        start.ArgumentList.Add("--agent-mode");
        start.ArgumentList.Add(RecoveryRequest.InputPipeArgument);
        start.ArgumentList.Add(inputHandle);
        return start;
    }

    internal static async Task<int> RunManualAsync(
        string agentExe, RecoveryRequest request, string? traceparent, CancellationToken cancellationToken)
    {
        using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        var start = BuildManualStartInfo(agentExe, input.GetClientHandleAsString(), traceparent);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the AI recovery process.");
        input.DisposeLocalCopyOfClientHandle();

        ConsoleCancelEventHandler cancelHandler = (_, e) => e.Cancel = true;
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var exited = process.WaitForExitAsync(cancellationToken);
            var sent = SendRequestAsync(input, request, cancellationToken);
            if (await Task.WhenAny(sent, exited).ConfigureAwait(false) == exited && !sent.IsCompleted)
            {
                await input.DisposeAsync().ConfigureAwait(false);
                try
                {
                    await sent.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // The child has already exited; its failure takes precedence over the closed pipe.
                }
                await exited.ConfigureAwait(false);
                if (process.ExitCode != 0)
                    return process.ExitCode;
                throw new InvalidOperationException("AI recovery exited before accepting its input.");
            }

            await sent.ConfigureAwait(false);
            await exited.ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return 130;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task SendRequestAsync(
        AnonymousPipeServerStream input, RecoveryRequest request, CancellationToken cancellationToken)
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
}
