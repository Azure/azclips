using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal sealed class CertificateOperationPublisher(IEventPublisher inner, CancellationToken token) : IEventPublisher
{
    public CancellationToken Token => token;
    public Action Cancel => inner.Cancel;

    public Task Publish(string id, CancellationToken cancellation, Func<object> message) => inner.Publish(id, cancellation, message);
    public Task Publish(string id, CancellationToken cancellation) => inner.Publish(id, cancellation);
    public Task Publish(string id, CancellationToken cancellation, string text) => inner.Publish(id, cancellation, text);
    public Task Publish(string id, CancellationToken cancellation, string text, HttpRequestMessage request) => inner.Publish(id, cancellation, text, request);
    public Task Publish(string id, CancellationToken cancellation, string text, HttpResponseMessage response) => inner.Publish(id, cancellation, text, response);
    public Task Publish(string id, CancellationToken cancellation, string text, double magnitude) => inner.Publish(id, cancellation, text, magnitude);
    public Task Publish(string id, CancellationToken cancellation, string text, double magnitude, HttpRequestMessage request) => inner.Publish(id, cancellation, text, magnitude, request);
    public Task Publish(string id, CancellationToken cancellation, string text, double magnitude, HttpResponseMessage response) => inner.Publish(id, cancellation, text, magnitude, response);
    public Task Publish(string id, CancellationToken cancellation, HttpRequestMessage request) => inner.Publish(id, cancellation, request);
    public Task Publish(string id, CancellationToken cancellation, HttpRequestMessage request, HttpResponseMessage response) => inner.Publish(id, cancellation, request, response);
    public Task Publish(string id, CancellationToken cancellation, HttpResponseMessage response) => inner.Publish(id, cancellation, response);
    public Task Publish(string id, CancellationToken cancellation, EventData message) => inner.Publish(id, cancellation, message);

    public Task Publish(string id, Func<object> message) => inner.Publish(id, Token, message);
    public Task Publish(string id) => inner.Publish(id, Token);
    public Task Publish(string id, string text) => inner.Publish(id, Token, text);
    public Task Publish(string id, string text, HttpRequestMessage request) => inner.Publish(id, Token, text, request);
    public Task Publish(string id, string text, HttpResponseMessage response) => inner.Publish(id, Token, text, response);
    public Task Publish(string id, string text, double magnitude) => inner.Publish(id, Token, text, magnitude);
    public Task Publish(string id, string text, double magnitude, HttpRequestMessage request) => inner.Publish(id, Token, text, magnitude, request);
    public Task Publish(string id, string text, double magnitude, HttpResponseMessage response) => inner.Publish(id, Token, text, magnitude, response);
    public Task Publish(string id, HttpRequestMessage request) => inner.Publish(id, Token, request);
    public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) => inner.Publish(id, Token, request, response);
    public Task Publish(string id, HttpResponseMessage response) => inner.Publish(id, Token, response);
    public Task Publish(string id, EventData message) => inner.Publish(id, Token, message);
    public Task Publish(string id, Uri uri) => inner.Publish(id, Token, uri.ToString());
}