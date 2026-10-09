// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Core.Abstractions.Runtime;

    /// <summary>
    /// Terminal factory that sends runtime pipeline requests through named HttpClient instances.
    /// </summary>
    public sealed class HttpClientTransportFactory : ISendAsyncTerminalFactory, ISendAsync
    {
        public const string SensitiveClientName = "Azclips.Sensitive";

        // Resolved once (this factory is a DI singleton) so each send reuses a stable client.
        // The named clients are backed by handlers with an infinite lifetime, so there is no
        // rotation to respect by re-creating the client per request.
        private readonly HttpClient defaultClient;
        private readonly HttpClient sensitiveClient;

        public HttpClientTransportFactory(IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            defaultClient = httpClientFactory.CreateClient(string.Empty);
            sensitiveClient = httpClientFactory.CreateClient(SensitiveClientName);
        }

        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
        {
            HttpClient transport = request.HasSensitiveDiagnostics() ? sensitiveClient : defaultClient;
            return transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, callback.Token);
        }
    }
}
