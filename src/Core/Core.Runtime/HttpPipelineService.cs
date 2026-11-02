// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime;

public class HttpPipelineService(
    IHttpTerminateHandler httpTerminateHandler,
    ITracingInterceptor tracingInterceptor,
    IUserAgentHandler userAgentHandler,
    IClientRequestIdHandler clientRequestIdHandler) : IHttpPipelineService
{
    public SendAsyncStep HttpPipelineTerminatingStep => httpTerminateHandler.SendAsync;
    public SendAsyncStep HttpRequestMessageTracingStep => tracingInterceptor.SendAsync;
    public SendAsyncStep HttpAddUserAgentStep => userAgentHandler.SendAsync;
    public SendAsyncStep HttpAddClientRequestIdStep => clientRequestIdHandler.SendAsync;
}