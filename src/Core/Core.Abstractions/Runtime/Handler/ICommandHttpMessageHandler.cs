// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions.Runtime.Handler;

public interface ICommandHttpMessageHandler
{
    /// <summary>
    /// Sends an HTTP request step with authentication.
    /// </summary>
    SendAsyncStep SendAsync { get; }
}
