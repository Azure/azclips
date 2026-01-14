namespace Core.Abstractions.Runtime
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using GetEventData = Func<object>;

    public delegate Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventListener callback);
    public delegate Task<HttpResponseMessage> SendAsyncStep(HttpRequestMessage request, IEventListener callback, ISendAsync next);
    public delegate Task SignalEvent(string id, CancellationToken token, GetEventData getEventData);
    public delegate Task Event(object message);
    public delegate void SynchEvent(object message);
}
