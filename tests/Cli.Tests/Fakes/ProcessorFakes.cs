// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using GetEventData = System.Func<object>;

namespace Cli.Tests;

/// <summary>
/// Shared offline fakes for the runtime context (pipeline, publisher, auth, output) used by
/// processor tests. Hand-written to match the no-mocking-framework style used across Cli.Tests.
/// </summary>
internal sealed class FakeOutputHandler : IOutputHandler
{
    public List<object> WrittenValues { get; } = new();

    public OutputStreamDelegate WriteOutput { get; set; }

    public FakeOutputHandler()
    {
        WriteOutput = (value, _) =>
        {
            WrittenValues.Add(value);
            return Task.CompletedTask;
        };
    }
}

internal sealed class FakeAuthHandler : IAuthHandler
{
    public FakeAuthHandler(Profile? profile) => Profile = profile;

    public Profile? Profile { get; set; }

    public AcquireTokenAsyncDelegate AcquireTokenAsyncDefault { get; set; } =
        (_, _, _, _, _) => throw new NotSupportedException("Token acquisition is not exercised in these offline tests.");

    public AcquireTokenAsyncDelegate? AcquireTokenAsyncOverride { get; set; }

    public bool ApplyOverride { get; set; }

    public SendAsyncStep SendAsync => (request, callback, next) => next.SendAsync(request, callback);
}

internal sealed class FakeHttpPipeline : IRuntimeHttpPipeline
{
    private readonly Func<HttpRequestMessage, IEventPublisher, HttpResponseMessage> _responder;

    public FakeHttpPipeline(Func<HttpRequestMessage, IEventPublisher, HttpResponseMessage> responder)
        => _responder = responder;

    public int SendCount { get; private set; }

    public int PollingInterval { get; set; }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
    {
        SendCount++;
        return Task.FromResult(_responder(request, callback));
    }

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

internal sealed class FakeEventPublisher : IEventPublisher
{
    public CancellationToken Token => CancellationToken.None;
    public Action Cancel => () => { };

    /// <summary>Event ids published through this publisher, in order, for assertions.</summary>
    public List<string> PublishedIds { get; } = new();

    public Task Publish(string id, CancellationToken token, GetEventData createMessage) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, string messageText) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, CancellationToken token, EventData message) { PublishedIds.Add(id); return Task.CompletedTask; }

    public Task Publish(string id, GetEventData createMessage) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, string messageText) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, string messageText, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, string messageText, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, string messageText, double magnitude) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, EventData message) { PublishedIds.Add(id); return Task.CompletedTask; }
    public Task Publish(string id, Uri uri) { PublishedIds.Add(id); return Task.CompletedTask; }
}
