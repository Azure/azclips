// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

public static class AgentProcessRunner
{
    public static async Task<int> RunAsync(
        string agentExe, RecoveryRequest request, TelemetryInvocationContext context, string fileId,
        CancellationToken cancellationToken, string? callerDirectory = null)
    {
        ConsoleCancelEventHandler cancelHandler = (_, e) => e.Cancel = true;
        Console.CancelKeyPress += cancelHandler;
        try
        {
            return await PipeProcessRunner.RunAsync(
                handle => AgentLauncher.BuildAgentStartInfo(agentExe, handle, context, fileId, callerDirectory), request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

}
