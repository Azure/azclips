using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Runtime;

public sealed class HttpClientTransportFactory : ISendAsyncTerminalFactory, ISendAsync
{
    public const string SensitiveClientName = "Azclips.Sensitive";

    private readonly HttpClient defaultClient;
    private readonly HttpClient sensitiveClient;

    public HttpClientTransportFactory(HttpClient defaultClient,
        [FromKeyedServices(SensitiveClientName)] HttpClient sensitiveClient)
    {
        this.defaultClient = defaultClient ?? throw new ArgumentNullException(nameof(defaultClient));
        this.sensitiveClient = sensitiveClient ?? throw new ArgumentNullException(nameof(sensitiveClient));
    }

    public ISendAsync Create() => this;

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) =>
        (request.HasSensitiveDiagnostics() ? sensitiveClient : defaultClient)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, callback.Token);

}