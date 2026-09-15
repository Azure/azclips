// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Test-only subclass that exposes the protected request-building + SelectOutput seams and
/// pre-populates JsonOptions so POST body serialization works without a runtime context.
/// </summary>
internal sealed class TestableCheckGroupMemberProcessor : CheckGroupMemberProcessor
{
    public TestableCheckGroupMemberProcessor()
    {
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    public Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    public TestableCheckGroupMemberProcessor WithPublisher(Core.Abstractions.Runtime.IEventPublisher publisher)
    {
        Publisher = publisher;
        return this;
    }
}
