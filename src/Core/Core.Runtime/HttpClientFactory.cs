// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Core.Abstractions.Runtime;
    using Microsoft.Azure.Azclips.ExtensionRuntime;

    /// <summary>
    /// Terminal factory that uses HttpClient to send requests.
    /// </summary>
    public class HttpClientFactory : ISendAsyncTerminalFactory, ISendAsync
    {
        private readonly HttpClient client;
        private readonly SensitiveHttpClient? sensitiveClient;

        public HttpClientFactory() : this(new HttpClient())
        {
        }

        public HttpClientFactory(HttpClient client) : this(client, null)
        {
        }

        public HttpClientFactory(HttpClient client, SensitiveHttpClient? sensitiveClient)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.sensitiveClient = sensitiveClient;
        }

        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
        {
            HttpClient transport = request.HasSensitiveDiagnostics()
                ? sensitiveClient ?? throw new InvalidOperationException("The host must configure a no-redirect SensitiveHttpClient before sending sensitive requests.")
                : client;
            return transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, callback.Token);
        }
    }
}
