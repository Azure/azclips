namespace Core.Abstractions;
using Core.Abstractions.Runtime;
public interface IHttpPipelineService
{
    SendAsyncStep HttpPipelineTerminatingStep { get; }
    SendAsyncStep HttpRequestMessageTracingStep { get; }
}