// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Moq;

namespace Cli.Tests;

/// <summary>
/// Shared Moq setup for Managed Identity orchestrator unit tests. Create the pipeline mock with
/// <see cref="MockBehavior.Strict"/> so any request a test did not set up throws <see cref="MockException"/>,
/// which proves the validation under test ran before the network.
/// </summary>
internal static class IdentityProcessorTestHelpers
{
    internal const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    internal const string MissingIdentityAddressMessage = "Provide either --ids, or both --name and --resource-group.";

    internal const string InvalidResourceIdMessage =
        "Invalid managed identity resource id 'not-a-resource-id'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{resourceName}.";

    /// <summary>Initializes <paramref name="processor"/> with <paramref name="mockPipeline"/> and returns it.</summary>
    internal static T Initialize<T>(T processor, Mock<IRuntimeHttpPipeline> mockPipeline) where T : IProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        processor.Init(new ProcessorContext(
            mockPipeline.Object,
            Mock.Of<IEventPublisher>(),
            Mock.Of<IAuthHandler>(a => a.Profile == profile),
            Mock.Of<IOutputHandler>(),
            Profile: profile));
        return processor;
    }

    /// <summary>Answers every GET with <paramref name="body"/>; any other request still throws on a strict mock.</summary>
    internal static void RespondToGet(this Mock<IRuntimeHttpPipeline> mockPipeline, string body) =>
        mockPipeline
            .Setup(p => p.SendAsync(It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get), It.IsAny<IEventPublisher>()))
            .ReturnsAsync((HttpRequestMessage request, IEventPublisher _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
}
