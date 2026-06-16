// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using GetEventData = System.Func<object>;

namespace Tests.Common.Stubs;

/// <summary>
/// Test event publisher that records published event ids (for assertions) and otherwise no-ops.
/// Lets pipeline steps publish lifecycle events without a live event bus.
/// </summary>
public sealed class StubEventPublisher : IEventPublisher
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
