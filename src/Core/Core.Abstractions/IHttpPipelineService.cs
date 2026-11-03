// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;
using Core.Abstractions.Runtime;
public interface IHttpPipelineService
{
    SendAsyncStep HttpPipelineTerminatingStep { get; }
    SendAsyncStep HttpRequestMessageTracingStep { get; }
    SendAsyncStep HttpAddUserAgentStep { get; }
    SendAsyncStep HttpAddClientRequestIdStep { get; }
}