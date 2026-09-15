// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Test-only subclass exposing the protected request-building seam and inherited runtime
/// fields so URL construction can be asserted without a live HTTP pipeline.
/// </summary>
internal sealed class TestableGetSignedInUserProcessor : GetSignedInUserProcessor
{
    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableGetSignedInUserProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}
