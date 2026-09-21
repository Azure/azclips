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
        HttpClient client;
        private static readonly HttpClient SensitiveClient = new(new HttpClientHandler { AllowAutoRedirect = false });

        public HttpClientFactory() : this(new HttpClient())
        {
        }

        public HttpClientFactory(HttpClient client) => this.client = client;

        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) =>
            (request.HasSensitiveDiagnostics() ? SensitiveClient : client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, callback.Token);
    }
}
