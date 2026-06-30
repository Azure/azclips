// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime;

public class HttpPipelineService(IHttpTerminateHandler httpTerminateHandler, ITracingInterceptor tracingInterceptor) : IHttpPipelineService
{
    public SendAsyncStep HttpPipelineTerminatingStep => httpTerminateHandler.SendAsync;
    public SendAsyncStep HttpRequestMessageTracingStep => tracingInterceptor.SendAsync;

    /// <summary>
    /// Pipeline step that stamps the User-Agent header on outgoing requests.
    /// Default is a no-op; CLI and PowerShell clients override this via DI.
    /// </summary>
    public virtual SendAsyncStep UserAgentStep => (request, callback, next) => next.SendAsync(request, callback);
}