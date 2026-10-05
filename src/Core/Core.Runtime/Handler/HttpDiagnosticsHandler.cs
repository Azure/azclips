using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Core.Runtime.Handler;

public sealed class HttpDiagnosticsHandler(HttpClient? protectedClient = null) : IHttpDiagnosticsHandler, IDisposable
{
    public SendAsyncStep SendAsync => async (request, publisher, next) =>
    {
        if (!request.HasSensitiveDiagnostics()) return await next.SendAsync(request, publisher);
        if (protectedClient is null)
            throw new InvalidOperationException("The host must configure a no-redirect transport in the HTTP diagnostics handler before sending sensitive requests.");
        try
        {
            var response = await protectedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, publisher.Token);
            response.RequestMessage ??= request;
            return response;
        }
        catch (HttpRequestException error)
        {
            throw new HttpRequestException("Sensitive HTTP request failed. Transport details are omitted.", inner: null, statusCode: error.StatusCode);
        }
    };

    public Task<string> FormatAsync(HttpRequestMessage request) => request.HasSensitiveDiagnostics()
        ? Task.FromResult($"HTTP request: {request.Method}. Sensitive details omitted.")
        : request.GetLog();

    public Task<string> FormatAsync(HttpResponseMessage response) => response.RequestMessage.HasSensitiveDiagnostics()
        ? Task.FromResult($"HTTP response: {(int)response.StatusCode}. Sensitive details omitted.")
        : response.GetLog();

    public void Dispose() => protectedClient?.Dispose();
}