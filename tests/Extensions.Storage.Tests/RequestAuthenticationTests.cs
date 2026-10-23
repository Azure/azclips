// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Moq;

namespace Extensions.Storage.Tests;

public class RequestAuthenticationTests
{
    [Fact]
    public async Task ConcurrentRequestsUseTheirOwnProfileScopeAndCancellationWithoutChangingOverrides()
    {
        var defaultProfile = new Profile { Name = "default", TenantId = "default-tenant" };
        var source = new Profile { Name = "source", TenantId = "source-tenant" };
        var destination = new Profile { Name = "destination", TenantId = "destination-tenant" };
        using var cancellation = new CancellationTokenSource();
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        var bothAcquiring = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        AcquireTokenAsyncDelegate originalOverride = (_, _, _, _, _) => Task.FromResult(Result("override", []));
        var auth = new AuthHandler
        {
            Profile = defaultProfile, ApplyOverride = true, AcquireTokenAsyncOverride = originalOverride,
            AcquireTokenAsyncDefault = async (scopes, tenant, _, token, profile) =>
            {
                Assert.Equal(cancellation.Token, token);
                Assert.Equal(profile!.TenantId, tenant);
                Assert.Equal(profile == source ? new[] { "source-scope" } : new[] { "destination-scope" }, scopes);
                if (Interlocked.Increment(ref acquisitions) == 2) bothAcquiring.SetResult();
                await release.Task.WaitAsync(token);
                return Result(profile.Name, scopes!, profile.TenantId);
            }
        };
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync((HttpRequestMessage request, IEventPublisher _) =>
            {
                Assert.True(request.Options.TryGetValue(RequestAuthentication.OptionsKey, out var authentication));
                Assert.Equal(authentication.Profile.Name, request.Headers.Authorization!.Parameter);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
        using var sourceRequest = new HttpRequestMessage(HttpMethod.Get, "https://source.blob.core.windows.net/container/blob");
        using var destinationRequest = new HttpRequestMessage(HttpMethod.Get, "https://destination.blob.core.windows.net/container/blob");
        sourceRequest.Options.Set(RequestAuthentication.OptionsKey, new RequestAuthentication(source, ["source-scope"]));
        destinationRequest.Options.Set(RequestAuthentication.OptionsKey, new RequestAuthentication(destination, ["destination-scope"]));
        var first = auth.SendAsync(sourceRequest, publisher.Object, terminal.Object);
        var second = auth.SendAsync(destinationRequest, publisher.Object, terminal.Object);
        try
        {
            await bothAcquiring.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.TrySetResult();
        }
        var responses = await Task.WhenAll(first, second);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(2, acquisitions);
        Assert.Same(defaultProfile, auth.Profile);
        Assert.True(auth.ApplyOverride);
        Assert.Same(originalOverride, auth.AcquireTokenAsyncOverride);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestsWithoutTheOptionKeepExistingOverrideBehavior(bool applyOverride)
    {
        var profile = new Profile { TenantId = "tenant" };
        var publisher = Mock.Of<IEventPublisher>();
        int defaultCalls = 0;
        int overrideCalls = 0;
        var auth = new AuthHandler
        {
            Profile = profile, ApplyOverride = applyOverride,
            AcquireTokenAsyncDefault = (scopes, tenant, _, token, selectedProfile) =>
            {
                Assert.Null(scopes);
                Assert.Equal(profile.TenantId, tenant);
                Assert.Same(profile, selectedProfile);
                Assert.Equal(CancellationToken.None, token);
                defaultCalls++;
                return Task.FromResult(Result("default", []));
            },
            AcquireTokenAsyncOverride = (_, _, _, _, _) =>
            {
                overrideCalls++;
                return Task.FromResult(Result("override", []));
            }
        };
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        using var response = await auth.SendAsync(request, publisher, terminal.Object);
        Assert.Equal(applyOverride ? 0 : 1, defaultCalls);
        Assert.Equal(applyOverride ? 1 : 0, overrideCalls);
        Assert.Equal(applyOverride ? "override" : "default", request.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task RequestProfileWorksWithoutChangingAnAbsentDefaultProfile()
    {
        var auth = new AuthHandler
        {
            AcquireTokenAsyncDefault = (scopes, _, _, _, _) => Task.FromResult(Result("request", scopes!))
        };
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/container/blob");
        request.Options.Set(RequestAuthentication.OptionsKey, new RequestAuthentication(new Profile { TenantId = "tenant" }, ["scope"]));
        using var response = await auth.SendAsync(request, Mock.Of<IEventPublisher>(), terminal.Object);
        Assert.Null(auth.Profile);
        Assert.Equal("request", request.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task UnconfiguredRequestWithoutAProfileStillSkipsAuthentication()
    {
        var auth = new AuthHandler
        {
            AcquireTokenAsyncDefault = (_, _, _, _, _) => throw new InvalidOperationException("Token acquisition must not occur.")
        };
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        using var response = await auth.SendAsync(request, Mock.Of<IEventPublisher>(), terminal.Object);
        Assert.Null(request.Headers.Authorization);
    }

    private static AuthResult Result(string token, string[] scopes, string tenant = "tenant") => new()
    {
        AccessToken = token, Scopes = scopes, TenantId = tenant,
        ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5), EntraObjectId = "fixture-object"
    };
}
