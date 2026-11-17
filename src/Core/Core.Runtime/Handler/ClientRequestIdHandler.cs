// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime.Handler;

public sealed class ClientRequestIdHandler : IClientRequestIdHandler
{
    internal const string HeaderName = RequestCorrelation.ClientRequestIdHeaderName;

    public SendAsyncStep SendAsync => (request, callback, next) =>
    {
        RequestCorrelation.EnsureClientRequestId(request);
        return next.SendAsync(request, callback);
    };
}
