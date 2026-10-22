// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GitHub.Copilot;

namespace Core.Agent.Telemetry;

internal sealed class AgentSdkLifetime(AgentInvocationContext context)
{
    private readonly object _gate = new();
    private Func<Task>? _disposeSession;
    private Func<Task>? _disposeClient;
    private Task? _cleanup;

    internal void SetClient(CopilotClient client) => _disposeClient = () => client.DisposeAsync().AsTask();
    internal void SetSession(CopilotSession session) => _disposeSession = () => session.DisposeAsync().AsTask();
    internal void SetClientCleanup(Func<Task> cleanup) => _disposeClient = cleanup;

    internal Task DisposeAsync()
    {
        lock (_gate)
            return _cleanup ??= DisposeCoreAsync();
    }

    private async Task DisposeCoreAsync()
    {
        using var teardown = context.EnterTeardown();
        context.CancelCurrentOperation();
        try
        {
            try
            {
                if (_disposeSession is not null)
                    await _disposeSession().ConfigureAwait(false);
            }
            finally
            {
                if (_disposeClient is not null)
                    await _disposeClient().ConfigureAwait(false);
            }
        }
        finally
        {
            // SDK cleanup may still invoke bound hooks/events. Only its actual completion,
            // not the host's bounded waiter returning, releases the invocation context.
            context.Dispose();
        }
    }
}
