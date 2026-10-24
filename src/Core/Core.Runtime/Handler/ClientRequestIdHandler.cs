// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime.Handler;

public sealed class ClientRequestIdHandler : IClientRequestIdHandler
{
    internal const string HeaderName = "x-ms-client-request-id";

    public SendAsyncStep SendAsync => (request, callback, next) =>
    {
        if (!request.Headers.Contains(HeaderName))
            request.Headers.TryAddWithoutValidation(HeaderName, Guid.NewGuid().ToString());

        return next.SendAsync(request, callback);
    };
}
