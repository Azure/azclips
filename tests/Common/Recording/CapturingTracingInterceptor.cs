// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

using Core.Recording;

namespace Tests.Common.Recording;

/// <summary>
/// Occupies the record/playback seam <c>Engine.CreateProcessorContext</c> reserves at the
/// tracing step ("trace every http request message, record/playback request during test").
/// The step runs after the auth step and before the retry/wire steps, so it sees the request
/// exactly as it would hit the network. It snapshots every request into <see cref="Captured"/>
/// and answers from <paramref name="responder"/> without calling the next step, so the network
/// and the retry handlers are never touched. Register it as the <see cref="ITracingInterceptor"/>
/// singleton before <c>AzclipsServiceProvider.Initialize</c>.
/// </summary>

/// No Inheritence for capturing traffic 
public sealed class CapturingTracingInterceptor(Func<HttpRequestMessage, HttpResponseMessage> responder) : ITracingInterceptor
{
    private readonly List<CapturedHttpRequest> _captured = [];

    /// <summary>Requests captured so far, in send order.</summary>
    public IReadOnlyList<CapturedHttpRequest> Captured => _captured;

    public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) =>
    {
        _captured.Add(await CapturedHttpRequest.SnapshotAsync(request));
        return responder(request);
    };
}
