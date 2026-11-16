// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Moq;

namespace Extensions.Resources.Tests;

/// <summary>Shared offline setup for Resources orchestrator unit tests.</summary>
internal static class ResourcesProcessorTestHelpers
{
    internal const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    /// <summary>
    /// Initializes <paramref name="processor"/> with a pipeline that throws on any request, so a test
    /// that gets its expected exception also proves the validation ran before the network.
    /// </summary>
    internal static T CreateProcessor<T>(T processor, Action<T> configure)
        where T : IProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = SubscriptionId };

        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.Setup(p => p.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
                .Throws(new InvalidOperationException("Unexpected request."));

        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);

        var auth = new Mock<IAuthHandler>();
        auth.SetupProperty(a => a.Profile, profile);
        auth.SetupGet(a => a.SendAsync).Returns((request, callback, next) => next.SendAsync(request, callback));

        var output = new Mock<IOutputHandler>();
        output.SetupGet(o => o.WriteOutput).Returns((_, _) => Task.CompletedTask);

        processor.Init(new ProcessorContext(pipeline.Object, publisher.Object, auth.Object, output.Object, Profile: profile));
        configure(processor);
        return processor;
    }
}