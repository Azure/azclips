// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Abstractions;

public record ProcessorContext
    (IRuntimeHttpPipeline HttpPipeline,
     IEventPublisher Publisher,
     IAuthHandler AuthHandler,
     IOutputHandler OutputHandler,
     Profile? Profile = null)
{
    public IHttpPipelineFactory? HttpPipelineFactory { get; init; }
}