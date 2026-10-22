// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Moq;
using Xunit;

namespace Cli.Tests;

/// <summary>Unit tests for <see cref="AuthHandler"/> token-acquisition wiring.</summary>
public class AuthHandlerTests
{
    // Regression test for the managed-identity refresh bug: the handler must pass the resolved
    // profile for the current invocation into token acquisition, so an explicit --profile override
    // refreshes against the intended identity instead of whichever profile happens to be active.
    [Fact]
    public async Task SendAsync_ShouldForwardResolvedProfileToTokenAcquirer()
    {
        // Arrange
        var resolvedProfile = new Profile
        {
            Name = "prod-mi",
            TenantId = string.Empty,
            SubscriptionId = "sub-x",
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.ManagedIdentity,
            ManagedIdentityId = "MSIClient@11111111-1111-1111-1111-111111111111",
        };

        Profile? capturedProfile = null;
        AcquireTokenAsyncDelegate acquirer = (scopes, tenantId, promptOnError, cancellationToken, profile) =>
        {
            capturedProfile = profile;
            return Task.FromResult(new AuthResult
            {
                AccessToken = "token-for-resolved-profile",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>(),
                TenantId = tenantId ?? string.Empty,
                CorrelationId = null,
            });
        };

        var handler = new AuthHandler
        {
            Profile = resolvedProfile,
            AcquireTokenAsyncDefault = acquirer,
            ApplyOverride = false,
        };

        var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/subscriptions/sub-x/resourcegroups?api-version=2022-12-01");

        var next = new Mock<ISendAsync>();
        next.Setup(n => n.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);

        // Act
        await handler.SendAsync(request, publisher.Object, next.Object);

        // Assert: the resolved profile flowed into token acquisition (not null / not the active profile)
        Assert.NotNull(capturedProfile);
        Assert.Same(resolvedProfile, capturedProfile);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-for-resolved-profile", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_ShouldPublishEnrichedProfile_AfterTokenAcquisition()
    {
        var resolvedProfile = new Profile
        {
            Name = "prod",
            TenantId = "tenant-1",
            SubscriptionId = "sub-1",
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.Browser,
        };

        AcquireTokenAsyncDelegate acquirer = (_, tenantId, _, _, _) =>
            Task.FromResult(new AuthResult
            {
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>(),
                TenantId = tenantId ?? string.Empty,
                EntraObjectId = "33333333-3333-3333-3333-333333333333",
                CorrelationId = null,
            });

        var handler = new AuthHandler
        {
            Profile = resolvedProfile,
            AcquireTokenAsyncDefault = acquirer,
            ApplyOverride = false,
        };

        Profile? publishedProfile = null;
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        publisher
            .Setup(p => p.Publish(Events.AuthAfterTokenAcquisition, It.IsAny<EventData>()))
            .Callback<string, EventData>((_, data) => publishedProfile = data.Payload as Profile)
            .Returns(Task.CompletedTask);

        var next = new Mock<ISendAsync>();
        next.Setup(n => n.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/"), publisher.Object, next.Object);

        Assert.NotNull(publishedProfile);
        Assert.Equal("33333333-3333-3333-3333-333333333333", publishedProfile!.Metadata.EntraObjectId);
        Assert.Equal("33333333-3333-3333-3333-333333333333", handler.Profile!.Metadata.EntraObjectId);
    }
}
