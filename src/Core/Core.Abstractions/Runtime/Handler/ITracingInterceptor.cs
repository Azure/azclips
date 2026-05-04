namespace Core.Abstractions.Runtime.Handler;

public interface ITracingInterceptor
{
    SendAsyncStep SendAsync { get; }
}