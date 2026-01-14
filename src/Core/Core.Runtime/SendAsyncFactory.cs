namespace Core.Runtime
{
    using Core.Abstractions.Runtime;
    using SendAsyncStepDelegate = System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>;

    /// <summary>
    /// Factory for creating ISendAsync pipeline steps.
    /// </summary>
    public class SendAsyncFactory : ISendAsyncFactory
    {
        public class Sender : ISendAsync
        {
            internal ISendAsync next = null!;
            internal SendAsyncStep implementation = null!;

            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventListener callback) => implementation(request, callback, next);
        }

        SendAsyncStep implementation;

        public SendAsyncFactory(SendAsyncStep implementation) => this.implementation = implementation;
        public ISendAsync Create(ISendAsync next) => new Sender { next = next, implementation = implementation };

        /// <summary>
        /// This translates a generic-defined delegate for a listener into one that fits our ISendAsync pattern.
        /// (Provided to support out-of-module delegation for Azure Cmdlets)
        /// </summary>
        /// <param name="step">The Pipeline Step as a delegate</param>
        public SendAsyncFactory(SendAsyncStepDelegate step) => this.implementation = (request, listener, next) =>
            step(
                request,
                listener.Token,
                listener.Cancel,
                (id, token, getEventData) => listener.Signal(id, token, () => {
                    var data = new EventData(getEventData);
                    data.Id = id;
                    data.Cancel = listener.Cancel;
                    data.RequestMessage = request;
                    return data;
                }),
                (req, token, cancel, listenerDelegate) => next.SendAsync(req, listener));

    }
}
