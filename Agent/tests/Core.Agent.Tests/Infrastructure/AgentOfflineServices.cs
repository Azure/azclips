// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Agent.Hosting;

namespace Core.Agent.Tests;

internal sealed class AgentOfflineHttp(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null) : HttpMessageHandler
{
    private int _requests;
    internal int Requests => Volatile.Read(ref _requests);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        return respond?.Invoke(request, cancellationToken)
            ?? throw new InvalidOperationException("An offline test attempted an unconfigured HTTP request.");
    }
}

/// <summary>Swallows output so the production event handler can run without a console.</summary>
internal sealed class SilentRenderer : IAgentRenderer
{
    public void OnText(string chunk) { }
    public void OnEvent(AgentEvent ev) { }
    public void OnSpinnerFrame(string frame, string verb) { }
    public void OnSpinnerStop() { }
}
