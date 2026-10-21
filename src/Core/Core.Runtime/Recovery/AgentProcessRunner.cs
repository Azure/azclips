// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

public static class AgentProcessRunner
{
    public static ProcessStartInfo BuildStartInfo(string agentExe, string inputHandle, string? traceparent)
    {
        var start = AgentLauncher.BuildAgentStartInfo(agentExe, [], null, traceparent);
        start.ArgumentList.Clear();
        start.ArgumentList.Add("--agent-mode");
        start.ArgumentList.Add(RecoveryRequest.InputPipeArgument);
        start.ArgumentList.Add(inputHandle);
        return start;
    }

    public static async Task<int> RunAsync(
        string agentExe, RecoveryRequest request, string? traceparent, CancellationToken cancellationToken)
    {
        ConsoleCancelEventHandler cancelHandler = (_, e) => e.Cancel = true;
        Console.CancelKeyPress += cancelHandler;
        try
        {
            return await PipeProcessRunner.RunAsync(
                handle => BuildStartInfo(agentExe, handle, traceparent), request, cancellationToken).ConfigureAwait(false);
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
