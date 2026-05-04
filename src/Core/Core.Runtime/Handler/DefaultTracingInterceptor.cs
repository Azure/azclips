using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Extensions.Runtime;

namespace Core.Runtime.Handler;

public class DefaultTracingInterceptor : ITracingInterceptor
{
    public SendAsyncStep SendAsync => (HttpRequestMessage request, IEventListener callback, ISendAsync next) =>
    {
        callback.Signal(Events.TracingHttpRequestMessage, request);
        return next.SendAsync(request, callback);
    };
}