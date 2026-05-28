namespace Core.Runtime
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Core.Abstractions.Runtime;

    /// <summary>
    /// Terminal factory that uses HttpClient to send requests.
    /// </summary>
    public class HttpClientFactory : ISendAsyncTerminalFactory, ISendAsync
    {
        HttpClient client;

        public HttpClientFactory() : this(new HttpClient())
        {
        }

        public HttpClientFactory(HttpClient client) => this.client = client;

        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) => client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, callback.Token);
    }
}
