// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Recovery;

internal sealed class ConsoleCancellationScope : IDisposable
{
    private readonly CancellationTokenSource source;
    private readonly ConsoleCancelEventHandler handler;

    internal ConsoleCancellationScope(CancellationToken cancellationToken)
    {
        source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handler = (_, e) =>
        {
            e.Cancel = true;
            source.Cancel();
        };
        Console.CancelKeyPress += handler;
    }

    internal CancellationToken Token => source.Token;

    public void Dispose()
    {
        Console.CancelKeyPress -= handler;
        source.Dispose();
    }
}
