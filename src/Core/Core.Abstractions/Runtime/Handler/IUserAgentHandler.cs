// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler;

public interface IUserAgentHandler
{
    /// <summary>
    /// Pipeline step that stamps the User-Agent header on outgoing HTTP requests.
    /// </summary>
    SendAsyncStep SendAsync { get; }
}
