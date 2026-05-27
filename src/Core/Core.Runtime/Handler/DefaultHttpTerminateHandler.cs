// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime.Handler;

public class DefaultHttpTerminateHandler : IHttpTerminateHandler
{
    /// <inheritdoc />
    public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) => await next.SendAsync(request, callback);
}
