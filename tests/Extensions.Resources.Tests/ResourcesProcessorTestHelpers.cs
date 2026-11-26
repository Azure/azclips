// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Moq;

namespace Extensions.Resources.Tests;

/// <summary>
/// Shared Moq setup for Resources orchestrator unit tests. Create the pipeline mock with
/// <see cref="MockBehavior.Strict"/> so any request a test did not set up throws <see cref="MockException"/>,
/// which proves the validation under test ran before the network.
/// </summary>
internal static class ResourcesProcessorTestHelpers
{
    /// <summary>Initializes <paramref name="processor"/> with <paramref name="mockPipeline"/> and no signed-in profile, and returns it.</summary>
    internal static T Initialize<T>(T processor, Mock<IRuntimeHttpPipeline> mockPipeline) where T : IProcessor =>
        Initialize(processor, mockPipeline, profile: null);

    /// <summary>Initializes <paramref name="processor"/> with <paramref name="mockPipeline"/> and <paramref name="profile"/> as the signed-in profile, and returns it.</summary>
    internal static T Initialize<T>(T processor, Mock<IRuntimeHttpPipeline> mockPipeline, Profile? profile) where T : IProcessor
    {
        processor.Init(new ProcessorContext(
            mockPipeline.Object,
            Mock.Of<IEventPublisher>(),
            Mock.Of<IAuthHandler>(a => a.Profile == profile),
            Mock.Of<IOutputHandler>(),
            Profile: profile));
        return processor;
    }
}
