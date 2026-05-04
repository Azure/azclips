// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions.Runtime.Handler;

public interface IHttpTerminateHandler
{
    /// <summary>
    /// Interceptor for http pipeline for mocking scenarios
    /// </summary>
    SendAsyncStep SendAsync { get; }
}
