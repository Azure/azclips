// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Cirrus.Extensions.Graph.Processors;

namespace Extensions.Graph.Tests;

/// <summary>
/// Test-only subclass that exposes the protected request-building seam and the inherited
/// runtime fields so URL construction can be asserted without a live HTTP pipeline.
/// </summary>
internal sealed class TestableGetUserProcessor : GetUserProcessor
{
    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableGetUserProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}
