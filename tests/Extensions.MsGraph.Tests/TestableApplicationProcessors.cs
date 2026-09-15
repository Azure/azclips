// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Test-only subclasses that expose the protected request-building seam and the inherited
/// runtime fields so URL/body construction can be asserted without a live HTTP pipeline.
/// </summary>
internal sealed class TestableGetApplicationProcessor : GetApplicationProcessor
{
    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableGetApplicationProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}

internal sealed class TestableNewApplicationProcessor : NewApplicationProcessor
{
    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableNewApplicationProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}

internal sealed class TestableDeleteApplicationProcessor : DeleteApplicationProcessor
{
    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableDeleteApplicationProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}

internal sealed class TestableListApplicationProcessor : ListApplicationProcessor
{
    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableListApplicationProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}
