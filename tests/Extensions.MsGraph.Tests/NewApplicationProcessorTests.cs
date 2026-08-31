// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

public class NewApplicationProcessorTests
{
    private static TestableNewApplicationProcessor CreateProcessor(string displayName, string signInAudience = "")
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new TestableNewApplicationProcessor { DisplayName = displayName, SignInAudience = signInAudience }
            .WithPublisher(publisher.Object);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldPostToGraphV1ApplicationsEndpoint()
    {
        var processor = CreateProcessor("cirrus-preview-app");

        using HttpRequestMessage request = await processor.BuildRequestAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/applications", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldSerializeDisplayNameInBody()
    {
        var processor = CreateProcessor("cirrus-preview-app");

        using HttpRequestMessage request = await processor.BuildRequestAsync();
        string body = await request.Content!.ReadAsStringAsync();

        Assert.Contains("\"displayName\":\"cirrus-preview-app\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldOmitSignInAudienceWhenNotProvided()
    {
        var processor = CreateProcessor("cirrus-preview-app");

        using HttpRequestMessage request = await processor.BuildRequestAsync();
        string body = await request.Content!.ReadAsStringAsync();

        Assert.DoesNotContain("signInAudience", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRequestMessage_ShouldIncludeSignInAudienceWhenProvided()
    {
        var processor = CreateProcessor("cirrus-preview-app", "AzureADMyOrg");

        using HttpRequestMessage request = await processor.BuildRequestAsync();
        string body = await request.Content!.ReadAsStringAsync();

        Assert.Contains("\"signInAudience\":\"AzureADMyOrg\"", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRequestMessage_MissingDisplayName_ShouldThrow(string displayName)
    {
        var processor = CreateProcessor(displayName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.BuildRequestAsync());
    }
}
