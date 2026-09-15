// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions.Runtime;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Offline fake for <see cref="IRuntimeHttpPipeline"/> used by orchestrator tests. Captures every
/// outgoing <see cref="HttpRequestMessage"/> and returns responses supplied by a caller-provided
/// responder. Only <see cref="ISendAsync.SendAsync"/> is exercised in these tests; the pipeline
/// composition methods are not.
/// </summary>
internal sealed class FakeGraphHttpPipeline : IRuntimeHttpPipeline
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeGraphHttpPipeline(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<HttpRequestMessage> Sent { get; } = new();

    public int PollingInterval { get; set; }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
    {
        Sent.Add(request);
        return Task.FromResult(_responder(request));
    }

    /// <summary>
    /// Builds a canned <see cref="HttpResponseMessage"/> with a JSON body.
    /// </summary>
    public static HttpResponseMessage JsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    public IRuntimeHttpPipeline Prepend(ISendAsyncFactory item) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Append(SendAsyncStep item) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Prepend(SendAsyncStep item) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Append(IEnumerable<SendAsyncStep> items) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Prepend(IEnumerable<SendAsyncStep> items) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Append(ISendAsyncFactory item) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Prepend(IEnumerable<ISendAsyncFactory> items) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Append(IEnumerable<ISendAsyncFactory> items) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Append(SendAsyncStepExpand item) => throw new NotSupportedException();
    public IRuntimeHttpPipeline Prepend(SendAsyncStepExpand item) => throw new NotSupportedException();
}
