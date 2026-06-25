// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler;

public interface ITracingInterceptor
{
    SendAsyncStep SendAsync { get; }
}