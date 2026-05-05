using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Extensions.Runtime;

namespace Core.Runtime.Handler;

public class DefaultTracingInterceptor : ITracingInterceptor
{
    public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventListener callback, ISendAsync next) =>
    {
        await callback.Signal(Events.TracingHttpRequestMessage, request);
        return await next.SendAsync(request, callback);
    };
}