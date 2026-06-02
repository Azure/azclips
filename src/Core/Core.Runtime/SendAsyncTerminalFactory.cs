namespace Core.Runtime
{
    using Core.Abstractions.Runtime;

    /// <summary>
    /// Terminal factory that wraps an existing SendAsync delegate or ISendAsync instance.
    /// </summary>
    public class SendAsyncTerminalFactory : ISendAsyncTerminalFactory, ISendAsync
    {
        SendAsync implementation;

        public SendAsyncTerminalFactory(SendAsync implementation) => this.implementation = implementation;
        public SendAsyncTerminalFactory(ISendAsync implementation) => this.implementation = implementation.SendAsync;

        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) => implementation(request, callback);
    }
}
