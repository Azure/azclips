using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime;

public class HttpPipelineService(IHttpTerminateHandler httpTerminateHandler, ITracingInterceptor tracingInterceptor) : IHttpPipelineService
{
    public SendAsyncStep HttpPipelineTerminatingStep => httpTerminateHandler.SendAsync;
    public SendAsyncStep HttpRequestMessageTracingStep => tracingInterceptor.SendAsync;
}