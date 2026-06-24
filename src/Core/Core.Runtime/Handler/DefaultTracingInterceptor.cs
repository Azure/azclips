// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Cirrus.ExtensionRuntime;

namespace Core.Runtime.Handler;

public class DefaultTracingInterceptor : ITracingInterceptor
{
    public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) =>
    {
        await callback.Publish(Events.TracingHttpRequestMessage, request);
        return await next.SendAsync(request, callback);
    };
}



