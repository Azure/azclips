// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Forwards every event to the session's publisher, but with its own cancellation token, which the HTTP pipeline uses for
/// requests and retries (like KeyVault's <c>CertificateOperationPublisher</c>), and optionally tells
/// <see cref="DeploymentProgressReporter"/> which events were published.
/// </summary>
internal sealed class DeploymentProgressPublisher(IEventPublisher inner, CancellationToken token, Action<string>? onPublish = null) : IEventPublisher
{
    public CancellationToken Token => token;
    public Action Cancel => inner.Cancel;

    public Task Publish(string id, CancellationToken cancellation, Func<object> createMessage) => Seen(id, inner.Publish(id, cancellation, createMessage));
    public Task Publish(string id, CancellationToken cancellation) => Seen(id, inner.Publish(id, cancellation));
    public Task Publish(string id, CancellationToken cancellation, string messageText) => Seen(id, inner.Publish(id, cancellation, messageText));
    public Task Publish(string id, CancellationToken cancellation, string messageText, HttpRequestMessage request) => Seen(id, inner.Publish(id, cancellation, messageText, request));
    public Task Publish(string id, CancellationToken cancellation, string messageText, HttpResponseMessage response) => Seen(id, inner.Publish(id, cancellation, messageText, response));
    public Task Publish(string id, CancellationToken cancellation, string messageText, double magnitude) => Seen(id, inner.Publish(id, cancellation, messageText, magnitude));
    public Task Publish(string id, CancellationToken cancellation, string messageText, double magnitude, HttpRequestMessage request) => Seen(id, inner.Publish(id, cancellation, messageText, magnitude, request));
    public Task Publish(string id, CancellationToken cancellation, string messageText, double magnitude, HttpResponseMessage response) => Seen(id, inner.Publish(id, cancellation, messageText, magnitude, response));
    public Task Publish(string id, CancellationToken cancellation, HttpRequestMessage request) => Seen(id, inner.Publish(id, cancellation, request));
    public Task Publish(string id, CancellationToken cancellation, HttpRequestMessage request, HttpResponseMessage response) => Seen(id, inner.Publish(id, cancellation, request, response));
    public Task Publish(string id, CancellationToken cancellation, HttpResponseMessage response) => Seen(id, inner.Publish(id, cancellation, response));
    public Task Publish(string id, CancellationToken cancellation, EventData message) => Seen(id, inner.Publish(id, cancellation, message));

    public Task Publish(string id, Func<object> createMessage) => Publish(id, Token, createMessage);
    public Task Publish(string id) => Publish(id, Token);
    public Task Publish(string id, string messageText) => Publish(id, Token, messageText);
    public Task Publish(string id, string messageText, HttpRequestMessage request) => Publish(id, Token, messageText, request);
    public Task Publish(string id, string messageText, HttpResponseMessage response) => Publish(id, Token, messageText, response);
    public Task Publish(string id, string messageText, double magnitude) => Publish(id, Token, messageText, magnitude);
    public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) => Publish(id, Token, messageText, magnitude, request);
    public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) => Publish(id, Token, messageText, magnitude, response);
    public Task Publish(string id, HttpRequestMessage request) => Publish(id, Token, request);
    public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) => Publish(id, Token, request, response);
    public Task Publish(string id, HttpResponseMessage response) => Publish(id, Token, response);
    public Task Publish(string id, EventData message) => Publish(id, Token, message);
    public Task Publish(string id, Uri uri) => Publish(id, Token, uri.ToString());

    private Task Seen(string id, Task published)
    {
        onPublish?.Invoke(id);
        return published;
    }
}
