namespace Core.Abstractions.Runtime.Handler;

public interface IHttpDiagnosticsHandler
{
    SendAsyncStep SendAsync { get; }
    Task<string> FormatAsync(HttpRequestMessage request);
    Task<string> FormatAsync(HttpResponseMessage response);
}