// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler;

public interface IClientRequestIdHandler
{
    /// <summary>
    /// Pipeline step that stamps an ARM correlation ID on an outgoing HTTP request.
    /// </summary>
    SendAsyncStep SendAsync { get; }
}
