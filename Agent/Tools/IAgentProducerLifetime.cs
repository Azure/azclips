// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Agent.Tools;

public interface IAgentProducerLifetime
{
    CancellationToken StoppingToken { get; }
    IDisposable? TryEnterProducer(bool teardown = false);
}
