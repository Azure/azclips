// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime.Handler;

/// <summary>
/// Default no-op User-Agent handler. CLI and PowerShell clients register their own implementations.
/// </summary>
public class DefaultUserAgentHandler : IUserAgentHandler
{
    /// <inheritdoc />
    public SendAsyncStep SendAsync => (request, callback, next) => next.SendAsync(request, callback);
}
